using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class UserInputMessageSectionTest
    {
        [Fact]
        public async Task Test_RenderAsMessagesAsync_ShoulderRender()
        {
            // Arrange
            UserInputMessageSection section = new();
            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            // Act
            memory.SetValue("input", "hi");

            memory.SetValue("inputFiles", new List<InputFile>()
            {
                new(BinaryData.FromString("testData"), "image/png")
            });

            // Assert
            RenderedPromptSection<List<ChatMessage>> rendered = await section.RenderAsMessagesAsync(context, memory, manager, tokenizer, 200);
            var messageContentParts = rendered.Output[0].GetContent<List<MessageContentParts>>();

            Assert.Equal("hi", ((TextContentPart)messageContentParts[0]).Text);

            // the base64 string is an encoding of "hi"
            var imageUrl = $"data:image/png;base64,dGVzdERhdGE=";
            Assert.Equal(imageUrl, ((ImageContentPart)messageContentParts[1]).ImageUrl);

            Assert.Equal(86, rendered.Length);
        }
    }
}
