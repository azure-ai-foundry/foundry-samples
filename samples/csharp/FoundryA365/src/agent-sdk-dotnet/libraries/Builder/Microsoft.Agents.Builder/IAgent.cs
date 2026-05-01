// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an Agent that can receive incoming Activities.
    /// </summary>
    /// <remarks>
    /// A <see cref="IChannelAdapter"/> passes incoming Activities from the channel
    /// to the Agent's <see cref="OnTurnAsync(ITurnContext, CancellationToken)"/> method
    /// after the Middleware registered with the Adapter have executed.
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.AgentCallbackHandler"/>
    public interface IAgent
    {
        /// <summary>
        /// When implemented in an Agent, handles an incoming activity.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>The <paramref name="turnContext"/> provides information about the
        /// incoming Activity, and other data needed to process the activity.</remarks>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext"/>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext.SendActivityAsync(Microsoft.Agents.Core.Models.IActivity, System.Threading.CancellationToken)"/>
        Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
    }
}
