namespace HelloWorldA365.AgentLogic.ResponsesApi;

using Azure.Core;
using Azure.Identity;
using HelloWorldA365.Models;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// OpenAI Responses API-based implementation of AgentLogicService.
/// Uses MCP tool definitions directly via the Responses API's native MCP support.
/// </summary>
public class ResponsesApiAgentLogicService : IAgentLogicService
{
    private readonly AgentMetadata _agentMetadata;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly string _accessToken;
    private readonly List<McpServerConfig> _mcpServers;
    private readonly HttpClient _httpClient;

    public ResponsesApiAgentLogicService(
        AgentMetadata agent,
        IConfiguration configuration,
        ILogger logger,
        string accessToken,
        List<McpServerConfig> mcpServers)
    {
        _agentMetadata = agent ?? throw new ArgumentNullException(nameof(agent));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accessToken = accessToken;
        _mcpServers = mcpServers;

        _httpClient = new HttpClient();
    }

    public async Task NewActivityReceived(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var incomingText = turnContext.Activity.Text;
        _logger.LogInformation("New activity received (Responses API): {IncomingText}", incomingText);

        var sender = turnContext.Activity.From;

        if (turnContext.Activity.ChannelId == "email" || turnContext.Activity.ChannelId == "agents:email")
        {
            var subject = string.Empty;
            if (turnContext.Activity.ChannelData is JsonElement jsonElement && jsonElement.TryGetProperty("subject", out var subjectProperty))
            {
                subject = subjectProperty.GetString() ?? string.Empty;
            }
            incomingText = $"Please respond to this email From: {sender!.Id}\nSubject: {subject}\nMessage: {incomingText}";
        }
        else if (turnContext.Activity.ChannelId == "msteams")
        {
            incomingText = $"Respond to this chat message with chat id {turnContext.Activity.Conversation.Id} " +
                           $"From: {sender?.Name} ({sender?.Id})\n" +
                           $"Message: {incomingText}\n" +
                           "If user hasn't explicitly asked to send teams messages don't use teams mcp tool to respond, that causes double responses.";
        }
        else if (turnContext.Activity.Type == ActivityTypes.InstallationUpdate)
        {
            incomingText = $"You were just added as a digital worker. Please send an email to {sender!.Id} with information on what you can do.";
        }

        var conversationId = turnContext.Activity.Conversation?.Id ?? "default";
        var response = await InvokeResponsesApiAsync(incomingText, conversationId);

        if (turnContext.Activity.Type == ActivityTypes.Message)
        {
            // The Message handler opens a StreamingResponse via QueueInformativeUpdateAsync
            // and ends it with EndStreamAsync in a finally. We must queue a final text chunk
            // here (even when extraction yielded no assistant text, e.g. tool-call-only
            // outputs) so the channel doesn't render "No text was streamed".
            var finalText = string.IsNullOrWhiteSpace(response) ? "Done." : response;
            turnContext.StreamingResponse.QueueTextChunk(finalText);
        }
        else if (!string.IsNullOrEmpty(response))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }
    }

    public async Task<string> NewEmailReceived(string fromEmail, string subject, string messageBody)
    {
        var formattedMessage = $"Please respond to this email From: {fromEmail}\nSubject: {subject}\nMessage: {messageBody}";
        return await InvokeResponsesApiAsync(formattedMessage, $"email:{fromEmail}:{subject}");
    }

    public async Task<string> NewChatReceived(string chatId, string fromUser, string messageBody)
    {
        var formattedMessage = $"Respond to this chat message with chat id {chatId} " +
                               $"From: {fromUser}\nMessage: {messageBody}";
        return await InvokeResponsesApiAsync(formattedMessage, chatId);
    }

    public async Task HandleEmailNotificationAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity emailEvent)
    {
        var fromEmail = emailEvent.From.Id;
        var emailJson = JsonSerializer.Serialize(emailEvent, new JsonSerializerOptions { WriteIndented = true });
        var conversationId = turnContext.Activity.Conversation?.Id ?? "email-notification";
        var response = await InvokeResponsesApiAsync($"You received a new email. Please look at the email and return a response in html format. From: {fromEmail}\nEmail details:\n{emailJson}", conversationId);
        var responseActivity = EmailResponse.CreateEmailResponseActivity(response);

        _logger.LogInformation(
            "Outgoing email response activity - original ReplyToId={OriginalReplyToId}, ConversationId={ConversationId}",
            responseActivity.ReplyToId,
            responseActivity.Conversation?.Id);

        await turnContext.SendActivityAsync(responseActivity);
    }

    public Task HandleCommentNotificationAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity commentEvent)
    {
        _logger.LogInformation("Processing comment notification (Responses API)");
        return Task.CompletedTask;
    }

    public Task HandleInstallationUpdateAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity installationEvent)
    {
        _logger.LogInformation("Processing installation update (Responses API)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Invokes the OpenAI Responses API with MCP tools from the manifest.
    /// </summary>
    private async Task<string> InvokeResponsesApiAsync(string input, string conversationId)
    {
        var envVars = Environment.GetEnvironmentVariables();
        var envLines = new List<string>(envVars.Count);
        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            envLines.Add($"{entry.Key}={entry.Value}");
        }
        envLines.Sort(StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("Process environment variables ({Count}):{NewLine}{EnvVars}", envLines.Count, Environment.NewLine, string.Join(Environment.NewLine, envLines));

        var endpoint = _configuration["AzureOpenAIEndpoint"] ?? throw new InvalidOperationException("AzureOpenAIEndpoint not configured");
        var deployment = _configuration["ModelDeployment"] ?? throw new InvalidOperationException("ModelDeployment not configured");
        var instructions = AgentInstructions.GetInstructions(_agentMetadata);

        // Build MCP tool definitions from discovered servers
        var mcpTools = _mcpServers.Select(server => new
        {
            type = "mcp",
            server_label = server.McpServerName,
            server_url = server.Url,
            server_description = $"MCP server: {server.McpServerName}",
            require_approval = "never",
            headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {_accessToken}"
            }
        }).ToArray<object>();

        _logger.LogInformation("Invoking Responses API with {McpToolCount} MCP tool servers", mcpTools.Length);

        // Load previous_response_id for conversation continuity
        var previousResponseId = LoadPreviousResponseId(conversationId);
        if (previousResponseId != null)
        {
            _logger.LogInformation("Continuing conversation {ConversationId} with previous_response_id: {PreviousResponseId}", conversationId, previousResponseId);
        }

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = deployment,
            ["instructions"] = instructions,
            ["input"] = input,
            ["tools"] = mcpTools
        };

        if (previousResponseId != null)
        {
            requestBody["previous_response_id"] = previousResponseId;
        }

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogDebug("Responses API request: {Request}", json);

        // Use Azure AI Foundry Responses API endpoint (model specified in body)
        var requestUrl = $"{endpoint.TrimEnd('/')}/openai/responses?api-version=2025-03-01-preview";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // Fall back to Bearer token auth (e.g., with DefaultAzureCredential token)
        var instanceClientId = Environment.GetEnvironmentVariable("FOUNDRY_AGENT_DEFAULT_INSTANCE_CLIENT_ID")
            ?? throw new InvalidOperationException("FOUNDRY_AGENT_DEFAULT_INSTANCE_CLIENT_ID environment variable is not set.");
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = instanceClientId,
        });
        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }), CancellationToken.None);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Responses API call failed with status {StatusCode}: {Response}", response.StatusCode, responseContent);
            return $"I encountered an error processing your request. Status: {response.StatusCode}";
        }

        _logger.LogDebug("Responses API response: {Response}", responseContent);

        // Save the response id for conversation continuity
        SaveResponseId(conversationId, responseContent);

        return ExtractOutputText(responseContent);
    }

    private static string GetResponseStoreDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".a365agent");
    }

    private static string GetResponseIdFilePath(string conversationId)
    {
        // SHA-256 hash conversation ID to produce a fixed-length, filesystem-safe filename
        // and avoid PathTooLongException for long conversation IDs.
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(conversationId));
        var safeId = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return Path.Combine(GetResponseStoreDir(), $"{safeId}.responseid");
    }

    private string? LoadPreviousResponseId(string conversationId)
    {
        try
        {
            var filePath = GetResponseIdFilePath(conversationId);
            if (File.Exists(filePath))
            {
                var id = File.ReadAllText(filePath).Trim();
                return string.IsNullOrEmpty(id) ? null : id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load previous_response_id for conversation {ConversationId}", conversationId);
        }
        return null;
    }

    private void SaveResponseId(string conversationId, string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                var responseId = idProp.GetString();
                if (!string.IsNullOrEmpty(responseId))
                {
                    var dir = GetResponseStoreDir();
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(GetResponseIdFilePath(conversationId), responseId);
                    _logger.LogDebug("Saved response_id {ResponseId} for conversation {ConversationId}", responseId, conversationId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save response_id for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Extracts the final output text from the Responses API response JSON.
    /// </summary>
    private string ExtractOutputText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                var textParts = new StringBuilder();
                foreach (var item in output.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var type) && type.GetString() == "message")
                    {
                        if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var contentItem in content.EnumerateArray())
                            {
                                if (contentItem.TryGetProperty("type", out var contentType) &&
                                    contentType.GetString() == "output_text" &&
                                    contentItem.TryGetProperty("text", out var text))
                                {
                                    textParts.Append(text.GetString());
                                }
                            }
                        }
                    }
                }
                return textParts.ToString();
            }

            // Fallback: try to get a simple text response
            if (root.TryGetProperty("output_text", out var simpleText))
            {
                return simpleText.GetString() ?? string.Empty;
            }

            _logger.LogWarning("Could not extract output text from Responses API response");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Responses API response");
            return string.Empty;
        }
    }
}
