// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Function for feedback loop activites
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="feedbackLoopData">The feedback loop data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    [Obsolete("Use Core FeedbackLoopHandler and AgentApplication.OnFeedbackLoop instead")]
    public delegate Task FeedbackLoopHandler(ITurnContext turnContext, ITurnState turnState, FeedbackLoopData feedbackLoopData, CancellationToken cancellationToken);
}
