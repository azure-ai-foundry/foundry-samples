using Json.Schema;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.Validators;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.ValidatorsTests
{
    public class JsonResponseValidatorTests
    {
        [Fact]
        public async Task Test_NoSchema_ShouldSucceed()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            JsonResponseValidator validator = new();
            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant) { Content = "{\"foo\":\"bar\"}" }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(res.Valid);
        }

        [Fact]
        public async Task Test_WithSchema_ShouldSucceed()
        {
            JsonSchemaBuilder schema = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    (
                        "foo",
                        new JsonSchemaBuilder().Type(SchemaValueType.String)
                    )
                )
                .Required(new string[] { "foo" });

            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            JsonResponseValidator validator = new(schema.Build());
            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant) { Content = "{\"foo\":\"bar\"}" }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(res.Valid);
        }

        [Fact]
        public async Task Test_WithSchema_ShouldFailRequired()
        {
            JsonSchemaBuilder schema = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    (
                        "foo",
                        new JsonSchemaBuilder().Type(SchemaValueType.String)
                    )
                )
                .Required(new string[] { "foo" });

            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            JsonResponseValidator validator = new(schema.Build());
            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant) { Content = "{\"hello\":1}" }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }

        [Fact]
        public async Task Test_WithSchema_ShouldFailType()
        {
            JsonSchemaBuilder schema = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    (
                        "foo",
                        new JsonSchemaBuilder().Type(SchemaValueType.String)
                    )
                )
                .Required(new string[] { "foo" });

            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            JsonResponseValidator validator = new(schema.Build());
            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant) { Content = "{\"foo\":1}" }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }
    }
}
