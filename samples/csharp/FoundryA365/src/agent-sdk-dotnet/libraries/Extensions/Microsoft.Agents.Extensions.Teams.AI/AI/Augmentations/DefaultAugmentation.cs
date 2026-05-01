using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.Validators;
using Microsoft.Agents.Extensions.Teams.AI.State;

namespace Microsoft.Agents.Extensions.Teams.AI.Augmentations
{
    /// <summary>
    /// Default Augmentation
    /// </summary>
    public class DefaultAugmentation : IAugmentation
    {
        /// <summary>
        /// Creates an instance of `DefaultAugmentation`
        /// </summary>
        public DefaultAugmentation()
        {

        }

        /// <inheritdoc />
        public PromptSection? CreatePromptSection()
        {
            return null;
        }

        /// <inheritdoc />
        public async Task<Plan?> CreatePlanFromResponseAsync(ITurnContext context, ITurnState memory, PromptResponse response, CancellationToken cancellationToken = default)
        {
            PredictedSayCommand say = new(response.Message?.GetContent<string>() ?? "");

            if (response.Message != null)
            {
                ChatMessage message = new(ChatRole.Assistant)
                {
                    Context = response.Message!.Context,
                    Content = response.Message.Content
                };

                say.Response = message;
            }

            return await Task.FromResult(new Plan()
            {
                Commands = { say }
            });
        }

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
