using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Validators;

namespace Microsoft.Agents.Extensions.Teams.AI.Augmentations
{
    /// <summary>
    /// Creates an optional prompt section for the augmentation.
    /// </summary>
    public interface IAugmentation : IPromptResponseValidator
    {
        /// <summary>
        /// Creates an optional prompt section for the augmentation.
        /// </summary>
        /// <returns></returns>
        public PromptSection? CreatePromptSection();

        /// <summary>
        /// Creates a plan given validated response value.
        /// </summary>
        /// <param name="context">Context for the current turn of conversation.</param>
        /// <param name="memory">An interface for accessing state variables.</param>
        /// <param name="response">The validated and transformed response for the prompt.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns></returns>
        public Task<Plan?> CreatePlanFromResponseAsync(ITurnContext context, ITurnState memory, PromptResponse response, CancellationToken cancellationToken = default);
    }
}
