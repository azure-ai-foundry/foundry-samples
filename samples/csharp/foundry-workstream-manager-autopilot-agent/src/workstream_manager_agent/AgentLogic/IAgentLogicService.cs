namespace WorkstreamManager.AgentLogic;

using Microsoft.Agents.A365.Notifications.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;

public interface IAgentLogicService
{
    /// <summary>
    /// Handles document comment notification events (Word, Excel, PowerPoint)
    /// </summary>
    Task HandleCommentNotificationAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity commentEvent);

    /// <summary>
    /// Handles Teams message events
    /// </summary>
    Task HandleTeamsMessageAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity teamsEvent);

    /// <summary>
    /// Handles installation update events
    /// </summary>
    Task HandleInstallationUpdateAsync(ITurnContext turnContext, ITurnState turnState, AgentNotificationActivity installationEvent);

    /// <summary>
    /// Handles a standard activity protocol message
    /// </summary>
    /// <returns></returns>
    Task NewActivityReceived(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);
}

