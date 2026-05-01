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
    public class ActionResponseValidatorTests
    {
        [Fact]
        public async Task Test_FunctionWithParams_ShouldSucceed()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            ActionResponseValidator validator = new(new()
            {
                new("test", "test action")
                {
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder().Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                }
            });

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    FunctionCall = new("test", "{ \"foo\": \"bar\" }")
                }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(res.Valid);
        }

        [Fact]
        public async Task Test_FunctionWithMissingParams_ShouldFail()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            ActionResponseValidator validator = new(new()
            {
                new("test", "test action")
                {
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder().Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                }
            });

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    FunctionCall = new("test", "{ \"hello\": \"world\" }")
                }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }

        [Fact]
        public async Task Test_FunctionWithInvalidParams_ShouldFail()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            ActionResponseValidator validator = new(new()
            {
                new("test", "test action")
                {
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder().Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                }
            });

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    FunctionCall = new("test", "{ \"foo\": 1 }")
                }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }

        [Fact]
        public async Task Test_MissingAction_ShouldFail()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            ActionResponseValidator validator = new(new()
            {
                new("test", "test action")
                {
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder().Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                }
            });

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    FunctionCall = new("empty", "{ \"foo\": \"bar\" }")
                }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }

        [Fact]
        public async Task Test_TextMessageRequired_ShouldFail()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            ActionResponseValidator validator = new(new()
            {
                new("test", "test action")
                {
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder().Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                }
            }, true);

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant) { Content = "test" }
            };

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }
    }
}
