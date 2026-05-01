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
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DialogSkillBot
{
    public class SkillAdapterWithErrorHandler : CloudAdapter
    {
        private readonly ConversationState _conversationState;
        private readonly ILogger _logger;

        public SkillAdapterWithErrorHandler(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue,
            ILogger<CloudAdapter> logger,
            IStorage storage,
            ConversationState conversationState,
            IAgentHost agentHost,
            IConfiguration configuration)
            : base(channelServiceClientFactory, activityTaskQueue, logger: logger, config: configuration)
        {
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            OnTurnError = HandleTurnError;
        }

        private async Task HandleTurnError(ITurnContext turnContext, Exception exception)
        {
            // Log any leaked exception from the application.
            _logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

            await SendErrorMessageAsync(turnContext, exception);
            await SendEoCToParentAsync(turnContext, exception);
            await ClearConversationStateAsync(turnContext);
        }

        private async Task SendErrorMessageAsync(ITurnContext turnContext, Exception exception)
        {
            try
            {
                // Send a message to the user.
                var errorMessageText = "The skill encountered an error or bug.";
                var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.IgnoringInput);
                await turnContext.SendActivityAsync(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception caught in SendErrorMessageAsync : {ex}");
            }
        }

        private async Task SendEoCToParentAsync(ITurnContext turnContext, Exception exception)
        {
            try
            {
                // Send an EndOfConversation activity to the skill caller with the error to end the conversation,
                // and let the caller decide what to do.
                var endOfConversation = Activity.CreateEndOfConversationActivity();
                endOfConversation.Code = "SkillError";
                endOfConversation.Text = exception.Message;
                await turnContext.SendActivityAsync(endOfConversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception caught in SendEoCToParentAsync : {ex}");
            }
        }

        private async Task ClearConversationStateAsync(ITurnContext turnContext)
        {
            try
            {
                // Delete the conversationState for the current conversation to prevent the
                // bot from getting stuck in a error-loop caused by being in a bad state.
                // ConversationState should be thought of as similar to "cookie-state" for a Web page.
                await _conversationState.DeleteStateAsync(turnContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception caught on attempting to Delete ConversationState : {ex}");
            }
        }
    }
}
