using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.Validators;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.ValidatorsTests
{
    public class DefaultResponseValidatorTests
    {
        [Fact]
        public async Task Test_ShouldSucceed()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            DefaultResponseValidator validator = new();
            PromptResponse promptResponse = new();

            var res = await validator.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(res.Valid);
        }
    }
}
