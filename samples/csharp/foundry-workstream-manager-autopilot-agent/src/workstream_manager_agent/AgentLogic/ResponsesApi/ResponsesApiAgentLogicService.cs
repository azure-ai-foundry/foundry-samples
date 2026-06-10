namespace WorkstreamManager.AgentLogic.ResponsesApi;

using WorkstreamManager.Models;
using WorkstreamManager.Services;
using WorkstreamManager.AgentLogic.ResponsesApi.Helpers;
using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

/// <summary>
/// OpenAI Responses API-based implementation of AgentLogicService.
/// Uses MCP tool definitions directly via the Responses API's native MCP support.
/// </summary>
public class ResponsesApiAgentLogicService : IAgentLogicService
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly ResponsesApiClient _responsesApiClient;
    private readonly WorkItemToolHandler _workItemTools;
    private readonly TeamsActivityHelper _teamsHelper;
    private readonly AccessControlService _accessControl;

    public ResponsesApiAgentLogicService(
        AgentMetadata agent,
        IConfiguration configuration,
        ILogger logger,
        string accessToken,
        List<McpServerConfig> mcpServers,
        string? graphAccessToken = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var agentMetadata = agent ?? throw new ArgumentNullException(nameof(agent));

        var httpClient = new HttpClient();
        _responsesApiClient = new ResponsesApiClient(agentMetadata, _logger, _configuration, accessToken, mcpServers, httpClient);

        // Initialize WorkItemToolHandler
        WorkItemService? workItemService = null;
        var workItemsTableServiceUri = configuration["WorkItemsTableServiceUri"];
        if (!string.IsNullOrEmpty(workItemsTableServiceUri))
        {
            workItemService = new WorkItemService(configuration, new LoggerFactory().CreateLogger<WorkItemService>());
        }
        _workItemTools = new WorkItemToolHandler(agentMetadata, _logger, graphAccessToken, httpClient, workItemService);
        _teamsHelper = new TeamsActivityHelper(_logger);
        _accessControl = new AccessControlService(agentMetadata, _logger, _configuration, graphAccessToken, httpClient, _teamsHelper, _workItemTools);
    }

    public async Task NewActivityReceived(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var incomingText = turnContext.Activity.Text;
        _logger.LogInformation("New activity received (Responses API): {IncomingText}", incomingText);

        var sender = turnContext.Activity.From;
        var rawUserMessage = incomingText ?? string.Empty;

        // Global AP tenant guard: if we can determine that the sender is from outside this
        // digital worker's tenant, return a deterministic canned response and skip LLM work.
        if (await _accessControl.TryHandleCrossTenantActivityAsync(turnContext, cancellationToken))
        {
            return;
        }

        if (turnContext.Activity.ChannelId == "msteams")
        {
            incomingText = $"Respond to this chat message with chat id {turnContext.Activity.Conversation.Id} " +
                           $"From: {sender?.Name} ({sender?.Id})\n" +
                           $"Message: {incomingText}";
        }
        else if (turnContext.Activity.Type == ActivityTypes.InstallationUpdate)
        {
            incomingText = $"You were just added as a digital worker. Please introduce yourself to {sender!.Id} with information on what you can do.";
        }

        var conversationId = turnContext.Activity.Conversation?.Id ?? "default";
        // Optional DM access control: in Teams 1:1 chats, only this digital worker's resolved
        // manager can trigger an LLM call. Everyone else gets a deterministic canned response.
        if (await _accessControl.TryHandleRestrictedDirectMessageAsync(turnContext, cancellationToken))
        {
            return;
        }

        // Optional group-chat access control: in Teams group chats, every participant must be
        // manager-approved (manager or allowlisted) before any LLM-based processing occurs.
        if (await _accessControl.TryHandleRestrictedGroupChatAsync(turnContext, cancellationToken))
        {
            return;
        }

        // Capture activity context for 📌 reaction on work item creation
        _workItemTools.SetCurrentActivityContext(turnContext.Activity.Id, turnContext.Activity.Conversation?.Id);

        var response = await _responsesApiClient.InvokeAsync(
            input: incomingText ?? string.Empty,
            conversationId: conversationId,
            additionalTools: _workItemTools.GetToolDefinitions(),
            localToolExecutor: _workItemTools.TryExecuteAsync);

        // For Teams group chat / channel we send a regular activity so the groupchat features
        // (@-mention entity + Teams reply blockquote) flow through unchanged. StreamingResponse
        // .QueueTextChunk delivers text only, not activity entities, so it cannot carry mention
        // markup. For 1:1 chats we use the streaming text path so the typing indicator the
        // Message handler opened in A365AgentApplication has a final chunk to render.
        //
        // The streaming path is additionally gated on the EnableStreamingUpdates config flag.
        // The Message handler only opens a stream (via QueueInformativeUpdateAsync) when that
        // flag is true; if we queued text here while the flag is false there would be no
        // opened stream to render into, so we must fall through to SendActivityAsync instead.
        var enableStreamingUpdates = _configuration.GetValue<bool>("EnableStreamingUpdates");
        var outChannelId = turnContext.Activity.ChannelId?.ToString();
        var outConversationType = turnContext.Activity.Conversation?.ConversationType;
        var outIsGroup = turnContext.Activity.Conversation?.IsGroup;
        var isTeamsGroupOrChannel = string.Equals(outChannelId, "msteams", StringComparison.OrdinalIgnoreCase)
            && (outIsGroup == true
                || string.Equals(outConversationType, "groupChat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outConversationType, "channel", StringComparison.OrdinalIgnoreCase));

        if (turnContext.Activity.Type == ActivityTypes.Message && !isTeamsGroupOrChannel && enableStreamingUpdates)
        {
            var finalText = string.IsNullOrWhiteSpace(response) ? "Done." : response;
            turnContext.StreamingResponse.QueueTextChunk(finalText);
        }
        else if (!string.IsNullOrEmpty(response))
        {
            var outboundActivity = _teamsHelper.BuildResponseActivity(turnContext, response);
            await turnContext.SendActivityAsync(outboundActivity, cancellationToken);
        }
    }

    public Task HandleCommentNotificationAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity commentEvent)
    {
        _logger.LogInformation("Processing comment notification (Responses API)");
        return Task.CompletedTask;
    }

    public Task HandleTeamsMessageAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity teamsEvent)
    {
        _logger.LogInformation("Processing Teams message (Responses API)");
        return Task.CompletedTask;
    }

    public Task HandleInstallationUpdateAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity installationEvent)
    {
        _logger.LogInformation("Processing installation update (Responses API)");
        return Task.CompletedTask;
    }

}

