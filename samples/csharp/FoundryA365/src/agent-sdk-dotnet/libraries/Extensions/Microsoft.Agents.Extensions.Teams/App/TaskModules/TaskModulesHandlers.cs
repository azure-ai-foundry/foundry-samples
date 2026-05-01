// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules
{
    /// <summary>
    /// Function for handling Task Module fetch events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data associated with the fetch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>An instance of TaskModuleResponse.</returns>
    public delegate Task<TaskModuleResponse> FetchHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Task Module submit events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data associated with the fetch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>An instance of TaskModuleResponse.</returns>
    public delegate Task<TaskModuleResponse> SubmitHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken);

}
