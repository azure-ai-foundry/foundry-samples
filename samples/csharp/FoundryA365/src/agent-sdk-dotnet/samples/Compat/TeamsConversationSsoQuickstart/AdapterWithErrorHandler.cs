// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.Builder.Compat;

namespace TeamsConversationSsoQuickstart
{
    public class AdapterWithErrorHandler : CloudAdapter
    {
        public AdapterWithErrorHandler(
            IChannelServiceClientFactory channelServiceClientFactory, 
            IActivityTaskQueue activityTaskQueue,
            IConfiguration configuration,
            ILogger<CloudAdapter> logger,
            IStorage storage,
            ConversationState conversationState)
            : base(channelServiceClientFactory, activityTaskQueue, logger: logger, config: configuration)
        {
            base.Use(new TeamsSSOTokenExchangeMiddleware(storage, configuration["ConnectionName"]));

            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

                // Uncomment below commented line for local debugging..
                // await turnContext.SendActivityAsync($"Sorry, it looks like something went wrong. Exception Caught: {exception.Message}");

                if (conversationState != null)
                {
                    try
                    {
                        // Delete the conversationState for the current conversation to prevent the
                        // bot from getting stuck in a error-loop caused by being in a bad state.
                        // ConversationState should be thought of as similar to "cookie-state" in a Web pages.
                        await conversationState.DeleteStateAsync(turnContext);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"Exception caught on attempting to Delete ConversationState : {e.Message}");
                    }
                }

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync(
                    "OnTurnError Trace",
                    exception.Message,
                    "https://www.botframework.com/schemas/error",
                    "TurnError");
            };
        }
    }
}