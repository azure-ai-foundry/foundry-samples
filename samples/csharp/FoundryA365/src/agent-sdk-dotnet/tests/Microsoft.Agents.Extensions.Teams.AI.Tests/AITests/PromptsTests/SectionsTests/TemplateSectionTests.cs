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
    public class TemplateSectionTests
    {
        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRenderWithFunction()
        {
            TemplateSection section = new("this is a test message: {{getMessage}}", ChatRole.User);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            manager.AddFunction("getMessage", async (context, memory, functions, tokenizer, args) =>
            {
                return await Task.FromResult("Hello World!");
            });

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 10);

            Assert.Equal("this is a test message: Hello World!", rendered.Output);
            Assert.Equal(9, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRenderWithFunction_WithWhiteSpace()
        {
            TemplateSection section = new("this is a test message: {{ getMessage }}", ChatRole.User);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            manager.AddFunction("getMessage", async (context, memory, functions, tokenizer, args) =>
            {
                return await Task.FromResult("Hello World!");
            });

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 10);

            Assert.Equal("this is a test message: Hello World!", rendered.Output);
            Assert.Equal(9, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRenderWithFunctionArgs()
        {
            TemplateSection section = new("this is a test message: {{getMessage 'my param'}}", ChatRole.User);
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            manager.AddFunction("getMessage", async (context, memory, functions, tokenizer, args) =>
            {
                return await Task.FromResult($"your param is: {args.First()}");
            });

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context.Object, memory, manager, tokenizer, 15);

            Assert.Equal("this is a test message: your param is: my param", rendered.Output);
            Assert.Equal(12, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRenderWithVariable()
        {
            TemplateSection section = new("this is a test message: {{$message}}", ChatRole.User);
            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            memory.SetValue("message", "Hello World!");

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context, memory, manager, tokenizer, 15);

            Assert.Equal("this is a test message: \"Hello World!\"", rendered.Output);
            Assert.Equal(10, rendered.Length);
        }

        [Fact]
        public async Task Test_RenderAsTextAsync_ShouldRenderWithVariable_WithWhitespace()
        {
            TemplateSection section = new("this is a test message: {{ $message }}", ChatRole.User);
            var context = TurnStateConfig.CreateConfiguredTurnContext();
            var storage = new MemoryStorage();
            var memory = new TurnState(storage);
            await memory.LoadStateAsync(context);
            GPTTokenizer tokenizer = new();
            PromptManager manager = new();

            memory.SetValue("message", "Hello World!");

            RenderedPromptSection<string> rendered = await section.RenderAsTextAsync(context, memory, manager, tokenizer, 15);

            Assert.Equal("this is a test message: \"Hello World!\"", rendered.Output);
            Assert.Equal(10, rendered.Length);
        }
    }
}
