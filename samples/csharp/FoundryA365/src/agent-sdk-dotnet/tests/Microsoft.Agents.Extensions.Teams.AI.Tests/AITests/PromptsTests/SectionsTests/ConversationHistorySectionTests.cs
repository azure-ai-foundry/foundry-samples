using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Moq;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class ConversationHistorySectionTests
    {
        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRender()
        {
            // Arrange
            ConversationHistorySection section = new("history");
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);

            memory.SetValue("history", new List<ChatMessage>()
            {
                new(ChatRole.System) { Content = "you are a unit test bot" },
                new(ChatRole.User) { Content = "hi" },
                new(ChatRole.Assistant) { Content = "hi, how may I assist you?" }
            });

            // Act
            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context, memory, manager, tokenizer, 50);

            // Assert
            Assert.Equal("assistant: hi, how may I assist you?\nuser: hi\nyou are a unit test bot", rendered.Output);
            Assert.Equal(21, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRenderEmpty()
        {
            // Arrange
            ConversationHistorySection section = new("history");
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            // Act
            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 50);

            // Assert
            Assert.Equal("", rendered.Output);
            Assert.Equal(0, rendered.Length);
        }


        [Fact]
        public async Task Test_RenderAsMessagesAsync_ShoulderRender()
        {
            // Arrange
            ConversationHistorySection section = new("history");
            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            // Act
            memory.SetValue("history", new List<ChatMessage>()
            {
                new(ChatRole.System) { Content = "you are a unit test bot" },
                new(ChatRole.User) { Content = "hi" },
                new(ChatRole.Assistant) { Content = "hi, how may I assist you?" }
            });

            // Assert
            RenderedPromptSection<List<ChatMessage>> rendered = await section.RenderAsMessagesAsync(context, memory, manager, tokenizer, 50);
            Assert.Equal("you are a unit test bot", rendered.Output[0].GetContent<string>());
            Assert.Equal("hi", rendered.Output[1].GetContent<string>());
            Assert.Equal("hi, how may I assist you?", rendered.Output[2].GetContent<string>());
            Assert.Equal(15, rendered.Length);
        }
    }
}
