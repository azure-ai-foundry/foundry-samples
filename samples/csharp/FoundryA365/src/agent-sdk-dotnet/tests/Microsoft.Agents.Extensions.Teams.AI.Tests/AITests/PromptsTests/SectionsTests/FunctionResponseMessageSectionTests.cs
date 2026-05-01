using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class FunctionResponseMessageSectionTests
    {
        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRender()
        {
            FunctionResponseMessageSection section = new("MyFunction", 27);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 10);
            Assert.Equal("user: MyFunction returned 27", rendered.Output);
            Assert.Equal(8, rendered.Length);
        }
    }
}
