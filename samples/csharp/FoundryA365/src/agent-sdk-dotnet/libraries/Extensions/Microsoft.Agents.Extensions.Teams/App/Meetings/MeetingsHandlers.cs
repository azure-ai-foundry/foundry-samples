// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.Meetings
{
    /// <summary>
    /// Function for handling Microsoft Teams meeting start events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="meeting">The details of the meeting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task MeetingStartHandler(ITurnContext turnContext, ITurnState turnState, MeetingStartEventDetails meeting, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Microsoft Teams meeting end events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="meeting">The details of the meeting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task MeetingEndHandler(ITurnContext turnContext, ITurnState turnState, MeetingEndEventDetails meeting, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Microsoft Teams meeting participants join or leave events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="meeting">The details of the meeting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task MeetingParticipantsEventHandler(ITurnContext turnContext, ITurnState turnState, MeetingParticipantsEventDetails meeting, CancellationToken cancellationToken);

}
