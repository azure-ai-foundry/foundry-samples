// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DialogRootBot
{
    public class AdapterWithErrorHandler : CloudAdapter
    {
        private readonly ConversationState _conversationState;
        private readonly ILogger _logger;
        private readonly IAgentHost _agentHost;

        public AdapterWithErrorHandler(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue,
            ILogger<CloudAdapter> logger,
            ConversationState conversationState,
            IAgentHost agentHost,
            IConfiguration configuration)
            : base(channelServiceClientFactory, activityTaskQueue, logger: logger, config: configuration)
        {
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            OnTurnError = HandleTurnError;
        }

        private async Task HandleTurnError(ITurnContext turnContext, Exception exception)
        {
            // Log any leaked exception from the application.
            // NOTE: In production environment, you should consider logging this to
            // Azure Application Insights. Visit https://aka.ms/bottelemetry to see how
            // to add telemetry capture to your bot.
            _logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

            await SendErrorMessageAsync(turnContext, exception);
            await EndSkillConversationAsync(turnContext);
        }

        private async Task SendErrorMessageAsync(ITurnContext turnContext, Exception exception)
        {
            try
            {
                // Send a message to the user.
                var errorMessageText = "The bot encountered an error or bug.";
                var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput);
                await turnContext.SendActivityAsync(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception caught in SendErrorMessageAsync : {ex}");
            }
        }

        private async Task EndSkillConversationAsync(ITurnContext turnContext)
        {
            try
            {
                await _agentHost.EndAllConversations(turnContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception caught in EndSkillConversationAsync : {ex}");
            }
        }
    }
}
