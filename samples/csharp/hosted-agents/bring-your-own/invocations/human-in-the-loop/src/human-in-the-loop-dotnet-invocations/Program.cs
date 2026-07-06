// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

// ---------------------------------------------------------------------------
// Foundry project configuration
// ---------------------------------------------------------------------------
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
    Console.Error.WriteLine(
        "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
        "to Application Insights. Set it to enable local telemetry. " +
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

var foundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is required.");
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is required.");

var projectClient = new AIProjectClient(new Uri(foundryEndpoint), new DefaultAzureCredential());

// Use the Responses API — not GetChatClient() (Chat Completions API is legacy).
var responsesClient = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForModel(deployment);

// Load persisted sessions from disk
SessionStore.LoadAllSessions();

InvocationsServer.Run<HumanInTheLoopHandler>(configure: builder =>
{
    builder.Services.AddSingleton(responsesClient);
});

// ──────────────────────────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Human-in-the-loop agent using the invocations protocol with Azure OpenAI.
/// Implements an approval-gate pattern: generates a proposal, pauses for
/// human review, and resumes after the human approves, revises, or rejects.
/// </summary>
public class HumanInTheLoopHandler : InvocationHandler
{
    private readonly ProjectResponsesClient _responsesClient;

    private const string SystemPrompt =
        "You are a professional assistant. The user will give you a task. " +
        "Generate a high-quality draft proposal that the user can review " +
        "and approve. Be detailed, well-structured, and ready for review.\n\n" +
        "If revision feedback is provided, incorporate it into an improved " +
        "version of the proposal.";

    public HumanInTheLoopHandler(ProjectResponsesClient responsesClient) => _responsesClient = responsesClient;

    // ── POST /invocations ──

    public override async Task HandleAsync(
        HttpRequest request,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        JsonElement body;
        try
        {
            body = await request.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (body.ValueKind != JsonValueKind.Object)
                throw new JsonException("body is not a JSON object");
        }
        catch (JsonException)
        {
            response.StatusCode = 400;
            await response.WriteAsJsonAsync(
                new
                {
                    error = "invalid_request",
                    message = "Request body must be a JSON object with either a \"task\" string (e.g. {\"task\": \"draft a project proposal\"}) to start a new proposal, or a \"decision\" of \"approve\" / \"revise\" / \"reject\" (e.g. {\"decision\": \"revise\", \"feedback\": \"make it shorter\"}).",
                },
                cancellationToken);
            return;
        }

        var sessionId = context.SessionId;
        var invocationId = context.InvocationId;

        var hasTask = body.TryGetProperty("task", out var taskProp);
        var hasDecision = body.TryGetProperty("decision", out var decisionProp);
        var task = hasTask ? taskProp.GetString() : null;
        var decision = hasDecision ? decisionProp.GetString() : null;

        if (!string.IsNullOrEmpty(task) && !string.IsNullOrEmpty(decision))
        {
            response.StatusCode = 400;
            await response.WriteAsJsonAsync(
                new { error = "Cannot provide both 'task' and 'decision' in the same request." },
                cancellationToken);
            return;
        }

        // --- New task submission ---
        if (!string.IsNullOrEmpty(task))
        {
            if (string.IsNullOrWhiteSpace(task))
            {
                response.StatusCode = 400;
                await response.WriteAsJsonAsync(new { error = "task cannot be empty" }, cancellationToken);
                return;
            }

            var existing = SessionStore.GetBySession(sessionId);
            if (existing is not null && existing.Status == "awaiting_approval")
            {
                response.StatusCode = 409;
                await response.WriteAsJsonAsync(new
                {
                    error = $"Session {sessionId} has a pending proposal. Approve, revise, or reject it before submitting a new task."
                }, cancellationToken);
                return;
            }

            var proposal = await GenerateProposalAsync(task, new List<RevisionEntry>(), cancellationToken);

            var session = new HitlSession
            {
                SessionId = sessionId,
                Status = "awaiting_approval",
                OriginalTask = task,
                Proposal = proposal,
                RevisionHistory = new List<RevisionEntry>(),
                InvocationId = invocationId,
                InvocationIds = new List<string> { invocationId },
            };

            SessionStore.TrackInvocation(invocationId, sessionId);
            SessionStore.Save(sessionId, session);

            await response.WriteAsJsonAsync(new
            {
                session_id = sessionId,
                invocation_id = invocationId,
                status = "awaiting_approval",
                proposal,
                revision_count = 0,
            }, cancellationToken);
            return;
        }

        // --- Decision on existing proposal ---
        if (!string.IsNullOrEmpty(decision))
        {
            var sessionLock = SessionStore.GetLock(sessionId);
            await sessionLock.WaitAsync(cancellationToken);
            try
            {
                var session = SessionStore.GetBySession(sessionId);
                if (session is null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsJsonAsync(
                        new { error = $"No pending session found for session_id={sessionId}" },
                        cancellationToken);
                    return;
                }

                if (session.Status != "awaiting_approval")
                {
                    response.StatusCode = 400;
                    await response.WriteAsJsonAsync(
                        new { error = $"Session is not awaiting approval (status={session.Status})" },
                        cancellationToken);
                    return;
                }

                if (decision is not ("approve" or "revise" or "reject"))
                {
                    response.StatusCode = 400;
                    await response.WriteAsJsonAsync(
                        new { error = $"Unknown decision: {decision}. Use 'approve', 'revise', or 'reject'." },
                        cancellationToken);
                    return;
                }

                var feedback = body.TryGetProperty("feedback", out var fbProp) ? fbProp.GetString() ?? "" : "";
                if (decision == "revise" && string.IsNullOrEmpty(feedback))
                {
                    response.StatusCode = 400;
                    await response.WriteAsJsonAsync(
                        new { error = "feedback is required for 'revise' decision" },
                        cancellationToken);
                    return;
                }

                // All validation passed — track the invocation
                session.InvocationId = invocationId;
                session.InvocationIds.Add(invocationId);
                SessionStore.TrackInvocation(invocationId, sessionId);

                if (decision == "approve")
                {
                    session.Status = "completed";
                    SessionStore.Save(sessionId, session);

                    await response.WriteAsJsonAsync(new
                    {
                        session_id = sessionId,
                        invocation_id = invocationId,
                        status = "completed",
                        final_output = session.Proposal,
                        revision_count = session.RevisionHistory.Count,
                    }, cancellationToken);
                    return;
                }

                if (decision == "revise")
                {
                    session.RevisionHistory.Add(new RevisionEntry
                    {
                        Proposal = session.Proposal,
                        Feedback = feedback,
                    });

                    var newProposal = await GenerateProposalAsync(
                        session.OriginalTask, session.RevisionHistory, cancellationToken);
                    session.Proposal = newProposal;
                    session.Status = "awaiting_approval";
                    SessionStore.Save(sessionId, session);

                    await response.WriteAsJsonAsync(new
                    {
                        session_id = sessionId,
                        invocation_id = invocationId,
                        status = "awaiting_approval",
                        proposal = newProposal,
                        revision_count = session.RevisionHistory.Count,
                    }, cancellationToken);
                    return;
                }

                // decision == "reject"
                session.Status = "rejected";
                SessionStore.Save(sessionId, session);

                await response.WriteAsJsonAsync(new
                {
                    session_id = sessionId,
                    invocation_id = invocationId,
                    status = "rejected",
                    revision_count = session.RevisionHistory.Count,
                }, cancellationToken);
            }
            finally
            {
                sessionLock.Release();
            }
            return;
        }

        // Neither task nor decision provided
        response.StatusCode = 400;
        await response.WriteAsJsonAsync(
            new { error = "invalid_request", message = "Request body must be a JSON object with either a \"task\" string (e.g. {\"task\": \"draft a project proposal\"}) to start a new proposal, or a \"decision\" of \"approve\" / \"revise\" / \"reject\" (e.g. {\"decision\": \"revise\", \"feedback\": \"make it shorter\"})." },
            cancellationToken);
    }

    // ── GET /invocations/{id} ──

    public override async Task GetAsync(
        string invocationId,
        HttpRequest request,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        var sessionId = SessionStore.GetSessionIdByInvocation(invocationId);
        var session = sessionId is not null ? SessionStore.GetBySession(sessionId) : null;

        if (session is null)
        {
            response.StatusCode = 404;
            await response.WriteAsJsonAsync(new { error = "not found" }, cancellationToken);
            return;
        }

        var responseData = new Dictionary<string, object>
        {
            ["session_id"] = session.SessionId,
            ["invocation_id"] = session.InvocationId,
            ["status"] = session.Status,
            ["original_task"] = session.OriginalTask,
            ["revision_count"] = session.RevisionHistory.Count,
        };

        if (session.Status == "awaiting_approval")
            responseData["proposal"] = session.Proposal;
        else if (session.Status == "completed")
            responseData["final_output"] = session.Proposal;

        await response.WriteAsJsonAsync(responseData, cancellationToken);
    }

    // ── POST /invocations/{id}/cancel ──

    public override async Task CancelAsync(
        string invocationId,
        HttpRequest request,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        var sessionId = SessionStore.GetSessionIdByInvocation(invocationId);
        var session = sessionId is not null ? SessionStore.GetBySession(sessionId) : null;

        if (session is null)
        {
            response.StatusCode = 404;
            await response.WriteAsJsonAsync(new { error = "not found" }, cancellationToken);
            return;
        }

        var sessionLock = SessionStore.GetLock(sessionId!);
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            // Check terminal state under lock to prevent TOCTOU race
            if (session.Status is "completed" or "rejected" or "cancelled")
            {
                await response.WriteAsJsonAsync(new
                {
                    session_id = sessionId,
                    invocation_id = invocationId,
                    status = session.Status,
                    error = "session already finalized",
                }, cancellationToken);
                return;
            }

            session.InvocationId = invocationId;
            session.InvocationIds.Add(invocationId);
            SessionStore.TrackInvocation(invocationId, sessionId!);
            session.Status = "cancelled";
            SessionStore.Save(sessionId!, session);
        }
        finally
        {
            sessionLock.Release();
        }

        await response.WriteAsJsonAsync(new
        {
            session_id = sessionId,
            invocation_id = invocationId,
            status = "cancelled",
        }, cancellationToken);
    }

    // ── LLM helper ──

    private async Task<string> GenerateProposalAsync(
        string task,
        List<RevisionEntry> revisionHistory,
        CancellationToken cancellationToken)
    {
        var options = new CreateResponseOptions { Instructions = SystemPrompt };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem($"Task: {task}"));

        foreach (var rev in revisionHistory)
        {
            options.InputItems.Add(ResponseItem.CreateAssistantMessageItem(rev.Proposal));
            options.InputItems.Add(ResponseItem.CreateUserMessageItem($"Revision feedback: {rev.Feedback}"));
        }

        var result = await _responsesClient.CreateResponseAsync(options, cancellationToken);
        return result.Value.GetOutputText() ?? "";
    }
}
