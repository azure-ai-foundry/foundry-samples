// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Compat
{
    /// <summary>
    ///  Middleware to normalize mention Entities from channels that apply &lt;at&gt; markup tags since they don't conform to expected values.
    ///  Agents that interact with Skype and/or teams should use this middleware if mentions are used.
    /// </summary>
    /// <description>
    ///  This will 
    ///  * remove mentions if they mention the recipient (aka the Agent) as that text can cause confusion with intent processing.
    ///  * remove extra &lt;at&gt; markup tags on mentions and in the activity.text.
    /// </description>
    public class NormalizeMentionsMiddleware : IMiddleware
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizeMentionsMiddleware"/> class.
        /// </summary>
        public NormalizeMentionsMiddleware()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the any recipient mentions should be removed.
        /// </summary>
        /// <value>If true, @mentions of the recipient will be completely stripped from the .text and .entities.</value>
        public bool RemoveRecipientMention { get; set; } = true;

        /// <summary>
        /// Middleware implementation which corrects Enity.Mention.Text to a value RemoveMentionText can work with.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="next">next middleware.</param>
        /// <param name="cancellationToken">cancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            turnContext.Activity.NormalizeMentions(RemoveRecipientMention);
            await next(cancellationToken).ConfigureAwait(false);
        }
    }
}
