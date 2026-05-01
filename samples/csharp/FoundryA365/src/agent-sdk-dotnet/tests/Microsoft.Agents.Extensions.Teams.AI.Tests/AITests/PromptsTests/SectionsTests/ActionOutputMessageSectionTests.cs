using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class ActionOutputMessageSectionTests
    {
        [Fact]
        public async Task Test_RenderAsMessages_NoHistory_ReturnsEmptyList()
        {
            // Arrange
            var historyVariable = "temp.history";
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            turnState.SetValue(historyVariable, new List<ChatMessage>() { });
            turnState.SetValue("actionOutputs", new Dictionary<string, string>());
            var section = new ActionOutputMessageSection(historyVariable);

            // Act
            var sections = await section.RenderAsMessagesAsync(turnContext, turnState, null!, null!, 0);

            // Assert
            Assert.Empty(sections.Output);
        }

        [Fact]
        public async Task Test_RenderAsMessages_HistoryWithoutActionCalls_ReturnsEmptyList()
        {
            // Arrange
            var historyVariable = "temp.history";
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            turnState.SetValue(historyVariable, new List<ChatMessage>() { new ChatMessage(ChatRole.Assistant) });
            turnState.SetValue("actionOutputs", new Dictionary<string, string>());
            var section = new ActionOutputMessageSection(historyVariable);

            // Act
            var sections = await section.RenderAsMessagesAsync(turnContext, turnState, null!, null!, 0);

            // Assert
            Assert.Empty(sections.Output);
        }

        [Fact]
        public async Task Test_RenderAsMessages_WithActionCalls_AddsToolMessage()
        {
            // Arrange
            var historyVariable = "temp.history";
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            turnState.SetValue(historyVariable, new List<ChatMessage>() { 
                new ChatMessage(ChatRole.Assistant) { 
                    ActionCalls = new List<ActionCall> { new ActionCall("testId", new ActionFunction("testName", "{}")) }
                }
            });
            turnState.SetValue("actionOutputs", new Dictionary<string, string>()
            {
                {  "testId", "testOutput" } 
            });
            var section = new ActionOutputMessageSection(historyVariable);

            // Act
            var sections = await section.RenderAsMessagesAsync(turnContext, turnState, null!, null!, 0);

            // Assert
            Assert.Single(sections.Output);
            Assert.Equal("testOutput", sections.Output[0].Content);
        }

        [Fact]
        public async Task Test_RenderAsMessages_WithInvalidActionCalls_AddsToolMessage_WithEmptyStringOutputContent()
        {
            // Arrange
            var historyVariable = "temp.history";
            var turnContext = TurnStateConfig.CreateConfiguredTurnContext();
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            turnState.SetValue(historyVariable, new List<ChatMessage>() {
                new ChatMessage(ChatRole.Assistant) {
                    ActionCalls = new List<ActionCall> { new ActionCall("testId", new ActionFunction("testName", "{}")) }
                }
            });
            turnState.SetValue("actionOutputs", new Dictionary<string, string>()
            {
                {  "InvalidTestId", "testOutput" }
            });
            var section = new ActionOutputMessageSection(historyVariable);

            // Act
            var sections = await section.RenderAsMessagesAsync(turnContext, turnState, null!, null!, 0);

            // Assert
            Assert.Single(sections.Output);
            Assert.Equal("", sections.Output[0].Content);
        }
    }
}
