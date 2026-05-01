// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Testing
{
    /// <summary>
    /// Validates agent replies using an <see cref="IChatClient"/> (AI model).
    /// The model is asked a yes/no question about the reply text; "yes" passes, "no" fails.
    /// </summary>
    public class SemanticValidator : IResponseValidator
    {
        private readonly IChatClient _chatClient;
        private readonly string _assertionPrompt;

        /// <summary>
        /// Initializes a new instance of <see cref="SemanticValidator"/>.
        /// </summary>
        /// <param name="chatClient">The AI client to use for evaluation.</param>
        /// <param name="assertionPrompt">A yes/no question about the agent reply, e.g. "Does this echo the user's message?"</param>
        public SemanticValidator(IChatClient chatClient, string assertionPrompt)
        {
            _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
            _assertionPrompt = assertionPrompt ?? throw new ArgumentNullException(nameof(assertionPrompt));
        }

        /// <inheritdoc/>
        public async Task ValidateAsync(IActivity reply, CancellationToken cancellationToken = default)
        {
            var replyText = string.IsNullOrEmpty(reply?.Text) ? "(non-text activity)" : reply.Text;

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "You are a test evaluator. Answer only \"yes\" or \"no\" — no other text."),
                new ChatMessage(ChatRole.User, $"Reply text: \"{replyText}\"\nQuestion: {_assertionPrompt}")
            };

            var options = new ChatOptions { MaxOutputTokens = 5 };

            var completion = await _chatClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            var response = completion?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            if (response.StartsWith("yes", StringComparison.Ordinal))
            {
                return;
            }
            else if (response.StartsWith("no", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Semantic validation failed.\nPrompt: {_assertionPrompt}\nAgent replied: {replyText}");
            }
            else
            {
                throw new InvalidOperationException(
                    $"SemanticValidator received unexpected LLM response: '{response}'. Expected 'yes' or 'no'.");
            }
        }
    }
}
