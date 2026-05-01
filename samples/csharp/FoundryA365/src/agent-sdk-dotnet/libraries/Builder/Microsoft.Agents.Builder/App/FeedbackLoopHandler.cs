// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Function for feedback loop activities
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="feedbackData">The feedback loop data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    public delegate Task FeedbackLoopHandler(ITurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken);
}
