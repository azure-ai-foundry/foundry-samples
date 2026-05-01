// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// The callback delegate for application code.
    /// </summary>
    /// <param name="turnContext">The turn context.</param>
    /// <param name="cancellationToken">The task cancellation token.</param>
    /// <seealso cref="Microsoft.Agents.Builder.IAgent.OnTurnAsync(Microsoft.Agents.Builder.ITurnContext, System.Threading.CancellationToken)"/>
    public delegate Task AgentCallbackHandler(ITurnContext turnContext, CancellationToken cancellationToken);
}
