using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Tests.App.TestUtils
{
    public static class TurnStateConfig
    {
        public static async Task<ITurnState> GetTurnStateWithConversationStateAsync(ITurnContext turnContext)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            Dictionary<string, JsonObject> dictionary = new Dictionary<string, JsonObject>();


            // Arrange
            var state = new TurnState(new MemoryStorage());
            IActivity activity = turnContext.Activity;
            string channelId = activity.ChannelId;
            string botId = activity.Recipient.Id;
            string conversationId = activity.Conversation.Id;
            string userId = activity.From.Id;

            await state.LoadStateAsync(turnContext, cancellationToken: CancellationToken.None);

            return state;
        }
        public static ITurnContext CreateConfiguredTurnContext()
        {
            return new TurnContext(new NotImplementedAdapter(), new Activity(
                text: "hello",
                channelId: "channelId",
                recipient: new() { Id = "recipientId" },
                conversation: new() { Id = "conversationId" },
                from: new() { Id = "fromId" }
            ));
        }
    }
}
