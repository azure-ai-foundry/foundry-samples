using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Moq;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class TextSectionTests
    {
        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRender()
        {
            TextSection section = new("this is a test section", ChatRole.User);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();
            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 10);

            Assert.Equal("this is a test section", rendered.Output);
            Assert.Equal(5, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldTruncate()
        {
            TextSection section = new("this is a test section", ChatRole.User);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();
            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 3);

            Assert.Equal("this is a", rendered.Output);
            Assert.Equal(3, rendered.Length);
        }
    }
}
