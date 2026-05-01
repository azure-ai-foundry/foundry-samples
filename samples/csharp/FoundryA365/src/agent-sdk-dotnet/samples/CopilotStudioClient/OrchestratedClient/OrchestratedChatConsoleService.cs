// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Models;

namespace OrchestratedClientSample;

/// <summary>
/// Console service that demonstrates externally orchestrated conversations with Copilot Studio.
/// The orchestrator controls the conversation flow: starting sessions, invoking tools, and forwarding user input.
/// </summary>
internal class OrchestratedChatConsoleService(OrchestratedClient orchestratedClient) : IHostedService
{
    private readonly string _conversationId = $"Orchestrated-{Guid.NewGuid()}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine($"Starting orchestrated conversation: {_conversationId}");
        Console.Write("\nagent> ");

        try
        {
            // Step 1: Start the orchestrated conversation
            var startRequest = new OrchestratedTurnRequest
            {
                Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
            };
            PrintRequest("StartConversation", startRequest);

            AgentStatePayload? lastState = null;
            await foreach (var response in orchestratedClient.ExecuteTurnAsync(_conversationId, startRequest, cancellationToken: cancellationToken))
            {
                System.Diagnostics.Trace.WriteLine($">>>>Duration: {sw.Elapsed.ToDurationString()}");
                sw.Restart();
                lastState = PrintResponse(response) ?? lastState;
            }

            PrintAgentState(lastState);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[EXCEPTION during StartConversation] {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
        }

        // Step 2: Enter the conversation loop — always expects JSON OrchestratedTurnRequest
        Console.WriteLine("\nPaste a JSON OrchestratedTurnRequest to send. Examples:");
        Console.WriteLine("  StartConversation: {\"orchestration\":{\"operation\":\"StartConversation\"}}");
        Console.WriteLine("  HandleUserResponse: {\"orchestration\":{\"operation\":\"HandleUserResponse\"},\"activity\":{\"type\":\"message\",\"text\":\"hello\"}}");
        Console.WriteLine("  InvokeTool: {\"orchestration\":{\"operation\":\"InvokeTool\",\"toolInputs\":{\"toolSchemaName\":\"myTool\"}},\"activity\":{\"type\":\"message\",\"text\":\"hi\"}}");
        Console.WriteLine("  ConversationUpdate: {\"orchestration\":{\"operation\":\"ConversationUpdate\"}}");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\nrequest> ");
            string input = Console.ReadLine()!;

            if (string.IsNullOrWhiteSpace(input))
                continue;

            try
            {
                var request = System.Text.Json.JsonSerializer.Deserialize<OrchestratedTurnRequest>(input, 
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (request is null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Parsed request is null.");
                    Console.ResetColor();
                    continue;
                }

                if (request.Orchestration is null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Request orchestration is missing.");
                    Console.ResetColor();
                    continue;
                }

                var operation = request.Orchestration.Operation.ToString();
                PrintRequest(operation, request);
                Console.Write("\nagent> ");
                sw.Restart();

                AgentStatePayload? lastState = null;
                await foreach (var response in orchestratedClient.ExecuteTurnAsync(_conversationId, request, cancellationToken: cancellationToken))
                {
                    System.Diagnostics.Trace.WriteLine($">>>>Duration: {sw.Elapsed.ToDurationString()}");
                    sw.Restart();
                    lastState = PrintResponse(response) ?? lastState;
                }

                PrintAgentState(lastState);
            }
            catch (System.Text.Json.JsonException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid JSON: {ex.Message}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[EXCEPTION] {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }
        }
        sw.Stop();
    }

    /// <summary>
    /// Prints the outgoing orchestration request to the console.
    /// </summary>
    private static void PrintRequest(string operation, OrchestratedTurnRequest request)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n>>> Sending [{operation}] request:");
        Console.WriteLine(ProtocolJsonSerializer.ToJson(request));
        Console.ResetColor();
    }

    /// <summary>
    /// Prints a single orchestrated response to the console. Returns AgentStatePayload if the response was a state response.
    /// </summary>
    private static AgentStatePayload? PrintResponse(OrchestratedResponse response)
    {
        // Dump the full response object to console for diagnostics
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n--- Response Type: {response.GetType().Name} ---");
        Console.ResetColor();

        switch (response)
        {
            case OrchestratedActivityResponse activityResponse:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(ProtocolJsonSerializer.ToJson(activityResponse.Activity));
                Console.ResetColor();
                PrintActivity(activityResponse.Activity);
                return null;

            case OrchestratedStateResponse stateResponse:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(ProtocolJsonSerializer.ToJson(stateResponse.AgentState));
                Console.ResetColor();
                return stateResponse.AgentState;

            case OrchestratedErrorResponse errorResponse:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ProtocolJsonSerializer.ToJson(errorResponse.Error));
                Console.WriteLine($"[ERROR] Code: {errorResponse.Error.Code}, Message: {errorResponse.Error.Message}");
                Console.ResetColor();
                return null;

            case OrchestratedEndResponse endResponse:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[END] Turn completed.");
                if (!string.IsNullOrEmpty(endResponse.Data))
                {
                    Console.WriteLine($"[END Data] {endResponse.Data}");
                }
                Console.ResetColor();
                return null;

            default:
                Console.WriteLine($"[unknown response type: {response}]");
                return null;
        }
    }

    private static void PrintActivity(IActivity act)
    {
        switch (act.Type)
        {
            case "message":
                Console.WriteLine(act.Text);
                if (act.SuggestedActions?.Actions.Count > 0)
                {
                    Console.WriteLine("Suggested actions:");
                    act.SuggestedActions.Actions.ToList().ForEach(action => Console.WriteLine($"  - {action.Text}"));
                }
                break;
            case "typing":
                Console.Write(".");
                break;
            case "event":
                Console.Write("+");
                break;
            default:
                Console.Write($"[{act.Type}]");
                break;
        }
    }

    private static void PrintAgentState(AgentStatePayload? state)
    {
        if (state is null) return;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n[Agent Status: {state.Status}]");
        if (state.EnabledToolSchemaNames.Length > 0)
        {
            Console.WriteLine($"[Enabled Tools: {string.Join(", ", state.EnabledToolSchemaNames)}]");
        }
        if (!string.IsNullOrEmpty(state.ResolvedAgentInstructions))
        {
            Console.WriteLine($"[Instructions: {state.ResolvedAgentInstructions}]");
        }
        Console.ResetColor();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.TraceInformation("Stopping");
        return Task.CompletedTask;
    }
}
