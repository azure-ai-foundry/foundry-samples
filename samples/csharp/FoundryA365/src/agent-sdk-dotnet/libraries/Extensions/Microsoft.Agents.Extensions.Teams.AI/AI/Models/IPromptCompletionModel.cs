using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Models
{
    /// <summary>
    /// An AI model that can be used to complete prompts.
    /// </summary>
    public interface IPromptCompletionModel
    {
        /// <summary>
        /// Completes a prompt.
        /// </summary>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="memory">An interface for accessing state values.</param>
        /// <param name="promptFunctions">Functions to use when rendering the prompt.</param>
        /// <param name="tokenizer">Tokenizer to use when rendering the prompt.</param>
        /// <param name="promptTemplate">Prompt template to complete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A `PromptResponse` with the status and message.</returns>
        Task<PromptResponse> CompletePromptAsync(
            ITurnContext turnContext,
            ITurnState memory,
            IPromptFunctions<List<string>> promptFunctions,
            ITokenizer tokenizer,
            PromptTemplate promptTemplate,
            CancellationToken cancellationToken);
    }
}
