using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Validators
{
    /// <summary>
    /// Default response validator that always returns true.
    /// </summary>
    public class DefaultResponseValidator : IPromptResponseValidator
    {
        /// <summary>
        /// Creates instance of `DefaultResponseValidator`
        /// </summary>
        public DefaultResponseValidator() : base() { }

        /// <inheritdoc />
        public async Task<Validation> ValidateResponseAsync(ITurnContext context, ITurnState memory, ITokenizer tokenizer, PromptResponse response, int remainingAttempts, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new Validation()
            {
                Valid = true
            });
        }
    }
}
