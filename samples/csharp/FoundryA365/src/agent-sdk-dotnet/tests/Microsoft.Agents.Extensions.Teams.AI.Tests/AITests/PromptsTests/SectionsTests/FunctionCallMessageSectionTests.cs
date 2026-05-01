using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class FunctionCallMessageSectionTests
    {
        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRender()
        {
            FunctionCallMessageSection section = new(new("MyFunction", ""));
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 20);
            Assert.Equal("assistant: {\"Name\":\"MyFunction\",\"Arguments\":\"\"}", rendered.Output);
            Assert.Equal(12, rendered.Length);
        }
    }
}
