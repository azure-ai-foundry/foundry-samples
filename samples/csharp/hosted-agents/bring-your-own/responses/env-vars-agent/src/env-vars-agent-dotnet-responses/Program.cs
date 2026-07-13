// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;

// Load environment variables from a .env file if present (for local development).
Env.NoClobber().TraversePath().Load();

// ──────────────────────────────────────────────────────────────────
// Startup — wire up the Foundry Responses client
// ──────────────────────────────────────────────────────────────────

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
    Console.Error.WriteLine(
        "[WARNING] APPLICATIONINSIGHTS_CONNECTION_STRING not set — traces will not be sent " +
        "to Application Insights. Set it to enable local telemetry. " +
        "(This variable is auto-injected in hosted Foundry containers — do not declare it in agent.manifest.yaml.)");

var foundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME environment variable is not set.");

var projectClient = new AIProjectClient(new Uri(foundryEndpoint), new DefaultAzureCredential());

// Use the Responses API via the Foundry project client.
var responsesClient = projectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForModel(deployment);

ResponsesServer.Run<EnvVarsHandler>(configure: builder =>
{
    builder.Services.AddSingleton(responsesClient);
});

// ──────────────────────────────────────────────────────────────────
// Handler — exposes a single tool that returns env var values, with
// a kind-aware safety policy:
//
//   kind = "metadata" | "target"  -> return the WHOLE value (non-secret
//                                    by Foundry connection convention)
//   kind = "credentials" (default) -> return only a SAFE FINGERPRINT
//                                    (length + first 4 chars + placeholder
//                                    check) — never the raw value
//
// The model picks the kind from the user's intent and the env vars
// declared in agent.manifest.yaml (see SystemPrompt below).
// ──────────────────────────────────────────────────────────────────

public class EnvVarsHandler : ResponseHandler
{
    // Maximum number of tool-call rounds before giving up. Bounds API cost
    // and request latency if the model gets stuck in a tool-call feedback loop.
    private const int MaxToolRounds = 5;

    private const string SystemPrompt =
        "You are an environment-variable inspector for a Foundry hosted agent. " +
        "Your only job is to call get_env_var(name, kind) and report back what " +
        "Foundry injected into the container at runtime.\n\n" +
        "How to choose `kind` — match the placeholder used in agent.manifest.yaml:\n" +
        "  - metadata     -> ${{connections.<name>.metadata.<key>}}     (plain, non-secret)\n" +
        "  - target       -> ${{connections.<name>.target}}             (endpoint URL, non-secret)\n" +
        "  - credentials  -> ${{connections.<name>.credentials.<key>}}  (secret)\n\n" +
        "If the user does not say which kind it is and you cannot tell from the " +
        "name, default to `credentials` — it is always safe to return a fingerprint " +
        "instead of a raw value.\n\n" +
        "When reporting results, be concise: surface what the tool returned " +
        "(value vs fingerprint) and whether the placeholder resolved.";

    private static readonly OpenAI.Responses.FunctionTool s_getEnvVarTool = ResponseTool.CreateFunctionTool(
        functionName: "get_env_var",
        functionDescription:
            "Reads an environment variable from the agent process. " +
            "If kind is 'metadata' or 'target', returns the whole value (those connection " +
            "fields are non-secret by Foundry convention). " +
            "If kind is 'credentials' (the default), returns only a safe fingerprint " +
            "(length, first 4 chars, placeholder check) — never the raw value.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "name": {
                    "type": "string",
                    "description": "Name of the environment variable to read, e.g. APP_REGION."
                },
                "kind": {
                    "type": "string",
                    "enum": ["metadata", "target", "credentials"],
                    "description": "Which connection-placeholder kind this env var was sourced from. metadata/target return the whole value; credentials returns only a fingerprint. Defaults to credentials when uncertain."
                }
            },
            "required": ["name"]
        }
        """),
        strictModeEnabled: false);

    private readonly ProjectResponsesClient _responsesClient;

    public EnvVarsHandler(ProjectResponsesClient responsesClient) => _responsesClient = responsesClient;

    public override IAsyncEnumerable<ResponseStreamEvent> CreateAsync(
        CreateResponse request,
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        return new TextResponse(context, request,
            createText: ct => GenerateTextAsync(context, ct));
    }

    private async Task<string> GenerateTextAsync(
        ResponseContext context,
        CancellationToken cancellationToken)
    {
        var userMessage = await context.GetInputTextAsync(cancellationToken: cancellationToken) ?? "";

        var options = new CreateResponseOptions { Instructions = SystemPrompt };
        options.Tools.Add(s_getEnvVarTool);
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(userMessage));

        // Function-call loop: keep asking the model until it returns a final
        // assistant message with no further function calls.
        for (int round = 0; round < MaxToolRounds; round++)
        {
            var result = await _responsesClient.CreateResponseAsync(options, cancellationToken);
            bool functionCalled = false;

            foreach (var responseItem in result.Value.OutputItems)
            {
                options.InputItems.Add(responseItem);
                if (responseItem is FunctionCallResponseItem functionCall)
                {
                    var toolOutput = ExecuteToolCall(
                        functionCall.FunctionName,
                        functionCall.FunctionArguments);
                    options.InputItems.Add(
                        ResponseItem.CreateFunctionCallOutputItem(functionCall.CallId, toolOutput));
                    functionCalled = true;
                }
            }

            if (!functionCalled)
                return result.Value.GetOutputText() ?? string.Empty;
        }

        return $"(Tool-call loop exceeded {MaxToolRounds} rounds without producing a final response.)";
    }

    private static string ExecuteToolCall(string functionName, BinaryData arguments)
    {
        if (functionName != "get_env_var")
            return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);

            if (!args.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
                return JsonSerializer.Serialize(new { error = "Missing or invalid 'name' argument." });

            var name = nameProp.GetString() ?? "";
            var kind = args.TryGetProperty("kind", out var kindProp) && kindProp.ValueKind == JsonValueKind.String
                ? (kindProp.GetString() ?? "credentials")
                : "credentials";

            return ReadEnvVar(name, kind);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid tool arguments: {ex.Message}" });
        }
    }

    private static string ReadEnvVar(string name, string kind)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (value is null)
            return JsonSerializer.Serialize(new { name, kind, status = "NOT_SET" });
        if (value.Length == 0)
            return JsonSerializer.Serialize(new { name, kind, status = "EMPTY" });

        var isPlaceholder = value.StartsWith("${{", StringComparison.Ordinal);
        var status = isPlaceholder ? "UNRESOLVED_PLACEHOLDER" : "RESOLVED";

        // Non-secret kinds: return the raw value verbatim. metadata and target
        // are stored as plain text on the connection itself.
        if (kind == "metadata" || kind == "target")
        {
            return JsonSerializer.Serialize(new
            {
                name,
                kind,
                status,
                length = value.Length,
                value,
            });
        }

        // Secret kind (credentials, or any unrecognized kind): fingerprint only.
        return JsonSerializer.Serialize(new
        {
            name,
            kind = "credentials",
            status,
            length = value.Length,
            head = value.Length >= 4 ? value[..4] : value,
        });
    }
}
