// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions
{
    /// <summary>
    /// Function for handling Message Extension submitAction events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="data">The data that was submitted.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of MessagingExtensionActionResponse.</returns>
    public delegate Task<MessagingExtensionActionResponse> SubmitActionHandlerAsync(ITurnContext turnContext, ITurnState turnState, object data, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension botMessagePreview edit events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="activityPreview">The activity that's being previewed by the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of MessagingExtensionActionResponse.</returns>
    public delegate Task<MessagingExtensionActionResponse> BotMessagePreviewEditHandlerAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension botMessagePreview send events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="activityPreview">The activity that's being previewed by the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task BotMessagePreviewSendHandler(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension fetchTask events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of TaskModuleResponse.</returns>
    public delegate Task<TaskModuleResponse> FetchTaskHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension query events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="query">The query parameters that were sent by the client.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of MessagingExtensionResult.</returns>
    public delegate Task<MessagingExtensionResult> QueryHandlerAsync(ITurnContext turnContext, ITurnState turnState, Query<IDictionary<string, object>> query, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension selecting item events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="item">The item that was selected.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of MessagingExtensionResult.</returns>
    public delegate Task<MessagingExtensionResult> SelectItemHandlerAsync(ITurnContext turnContext, ITurnState turnState, object item, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension link unfurling events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="url">The URL that should be unfurled.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of MessagingExtensionResult.</returns>
    public delegate Task<MessagingExtensionResult> QueryLinkHandlerAsync(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension configuring query setting url events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>An instance of MessagingExtensionResult.</returns>
    public delegate Task<MessagingExtensionResult> QueryUrlSettingHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension configuring settings events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="settings">The configuration settings that was submitted.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task ConfigureSettingsHandler(ITurnContext turnContext, ITurnState turnState, object settings, CancellationToken cancellationToken);

    /// <summary>
    /// Function for handling Message Extension clicking card button events.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="cardData">The card data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    public delegate Task CardButtonClickedHandler(ITurnContext turnContext, ITurnState turnState, object cardData, CancellationToken cancellationToken);
}
