using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.DataSources;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.PromptsTests.SectionsTests
{
    public class DataSourceSectionTests
    {
        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRender()
        {
            TextDataSource dataSource = new("test", "my text to use");
            DataSourceSection section = new(dataSource);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();
            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 10);

            Assert.Equal("my text to use", rendered.Output);
            Assert.Equal(4, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldTruncate()
        {
            TextDataSource dataSource = new("test", "my text to use");
            DataSourceSection section = new(dataSource);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();
            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 3);

            Assert.Equal("my text to", rendered.Output);
            Assert.Equal(3, rendered.Length);
        }
    }
}
