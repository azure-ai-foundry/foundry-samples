using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using Record = Microsoft.Agents.Extensions.Teams.AI.State.Record;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils
{
    public static class TurnStateConfig
    {
        public static async Task<TurnState> GetTurnStateWithConversationStateAsync(TurnContext turnContext)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            // Arrange
            var state = new TurnState();
            IActivity activity = turnContext.Activity;
            string? channelId = activity.ChannelId;
            string botId = activity.Recipient.Id;
            string conversationId = activity.Conversation.Id;
            string userId = activity.From.Id;

            string conversationKey = $"{channelId}/${botId}/conversations/${conversationId}";
            string userKey = $"{channelId}/${botId}/users/${userId}";

            var conversationState = new Record();
            var userState = new Record();

            Mock<IStorage> storage = new();
            var temp = new TempState();
            temp.SetValue("actionOutputs", new Dictionary<string, string>());
            storage.Setup(storage => storage.ReadAsync(new string[] { conversationKey, userKey }, It.IsAny<CancellationToken>())).Returns(() =>
            {
                IDictionary<string, object> items = new Dictionary<string, object>();
                items[conversationKey] = conversationState;
                items[userKey] = userState;
                items["temp"] = temp;
                return Task.FromResult(items);
            });

            await state.LoadStateAsync(turnContext);
            return state;
        }

        public static TurnContext CreateConfiguredTurnContext()
        {
            // Create a mock adapter
            var mockAdapter = new Mock<IChannelAdapter>();
            return new TurnContext(mockAdapter.Object, new Activity(
                text: "hello",
                channelId: "channelId",
                recipient: new() { Id = "recipientId" },
                conversation: new() { Id = "conversationId" },
                from: new() { Id = "fromId" }
            ));
        }
    }
}
