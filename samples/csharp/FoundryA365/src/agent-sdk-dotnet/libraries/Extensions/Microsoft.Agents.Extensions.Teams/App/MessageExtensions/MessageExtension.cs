// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions
{
    /// <summary>
    /// Constants for message extension invoke names
    /// </summary>
    public class MessageExtensionsInvokeNames
    {
        /// <summary>
        /// Fetch task invoke name
        /// </summary>
        public static readonly string FETCH_TASK_INVOKE_NAME = "composeExtension/fetchTask";
        /// <summary>
        /// Query invoke name
        /// </summary>
        public static readonly string QUERY_INVOKE_NAME = "composeExtension/query";
        /// <summary>
        /// Query link invoke name
        /// </summary>
        public static readonly string QUERY_LINK_INVOKE_NAME = "composeExtension/queryLink";
        /// <summary>
        /// Anonymous query link invoke name
        /// </summary>
        public static readonly string ANONYMOUS_QUERY_LINK_INVOKE_NAME = "composeExtension/anonymousQueryLink";
    }

    /// <summary>
    /// MessageExtensions class to enable fluent style registration of handlers related to Message Extensions.
    /// </summary>
    public class MessageExtension
    {
        private static readonly string SUBMIT_ACTION_INVOKE_NAME = "composeExtension/submitAction";
        private static readonly string SELECT_ITEM_INVOKE_NAME = "composeExtension/selectItem";
        private static readonly string CONFIGURE_SETTINGS = "composeExtension/setting";
        private static readonly string QUERY_SETTING_URL = "composeExtension/querySettingUrl";
        private static readonly string QUERY_CARD_BUTTON_CLICKED = "composeExtension/onCardButtonClicked";

        private readonly AgentApplication _app;

        /// <summary>
        /// Creates a new instance of the MessageExtensions class.
        /// </summary>
        /// <param name="app"></param> The top level application class to register handlers with.
        public MessageExtension(AgentApplication app)
        {
            this._app = app;
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(string commandId, SubmitActionHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), SUBMIT_ACTION_INVOKE_NAME);
            return OnSubmitAction(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(Regex commandIdPattern, SubmitActionHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), SUBMIT_ACTION_INVOKE_NAME);
            return OnSubmitAction(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(RouteSelector routeSelector, SubmitActionHandlerAsync handler)
        {
            MessagingExtensionAction? messagingExtensionAction;
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, SUBMIT_ACTION_INVOKE_NAME)
                    || (messagingExtensionAction = ProtocolJsonSerializer.ToObject<MessagingExtensionAction>(turnContext.Activity.Value)) == null)
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnSubmitAction() triggered for activity type: {turnContext.Activity.Type}");
                }

                MessagingExtensionActionResponse result = await handler(turnContext, turnState, messagingExtensionAction.Data, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(MultipleRouteSelector routeSelectors, SubmitActionHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnSubmitAction(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnSubmitAction(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnSubmitAction(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(string commandId, BotMessagePreviewEditHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), SUBMIT_ACTION_INVOKE_NAME, "edit");
            return OnAgentMessagePreviewEdit(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(Regex commandIdPattern, BotMessagePreviewEditHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), SUBMIT_ACTION_INVOKE_NAME, "edit");
            return OnAgentMessagePreviewEdit(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(RouteSelector routeSelector, BotMessagePreviewEditHandlerAsync handler)
        {
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                MessagingExtensionAction? messagingExtensionAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, SUBMIT_ACTION_INVOKE_NAME)
                    || (messagingExtensionAction = ProtocolJsonSerializer.ToObject<MessagingExtensionAction>(turnContext.Activity.Value)) == null
                    || !string.Equals(messagingExtensionAction.BotMessagePreviewAction, "edit"))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnAgentMessagePreviewEdit() triggered for activity type: {turnContext.Activity.Type}");
                }

                MessagingExtensionActionResponse result = await handler(turnContext, turnState, messagingExtensionAction.BotActivityPreview[0], cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(MultipleRouteSelector routeSelectors, BotMessagePreviewEditHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnAgentMessagePreviewEdit(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnAgentMessagePreviewEdit(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnAgentMessagePreviewEdit(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(string commandId, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), SUBMIT_ACTION_INVOKE_NAME, "send");
            return OnAgentMessagePreviewSend(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(Regex commandIdPattern, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), SUBMIT_ACTION_INVOKE_NAME, "send");
            return OnAgentMessagePreviewSend(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(RouteSelector routeSelector, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                MessagingExtensionAction? messagingExtensionAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, SUBMIT_ACTION_INVOKE_NAME)
                    || (messagingExtensionAction = ProtocolJsonSerializer.ToObject<MessagingExtensionAction>(turnContext.Activity.Value)) == null
                    || !string.Equals(messagingExtensionAction.BotMessagePreviewAction, "send"))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnAgentMessagePreviewSend() triggered for activity type: {turnContext.Activity.Type}");
                }

                Activity activityPreview = messagingExtensionAction.BotActivityPreview.Count > 0 ? messagingExtensionAction.BotActivityPreview[0] : new Activity();
                await handler(turnContext, turnState, activityPreview, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    MessagingExtensionActionResponse response = new();
                    var activity = Activity.CreateInvokeResponseActivity(response);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(MultipleRouteSelector routeSelectors, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnAgentMessagePreviewSend(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnAgentMessagePreviewSend(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnAgentMessagePreviewSend(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="commandId">ID of the commands to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(string commandId, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), MessageExtensionsInvokeNames.FETCH_TASK_INVOKE_NAME);
            return OnFetchTask(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the commands to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(Regex commandIdPattern, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), MessageExtensionsInvokeNames.FETCH_TASK_INVOKE_NAME);
            return OnFetchTask(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(RouteSelector routeSelector, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, MessageExtensionsInvokeNames.FETCH_TASK_INVOKE_NAME))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnFetchTask() triggered for activity type: {turnContext.Activity.Type}");
                }

                TaskModuleResponse result = await handler(turnContext, turnState, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(MultipleRouteSelector routeSelectors, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnFetchTask(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnFetchTask(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnFetchTask(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(string commandId, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), MessageExtensionsInvokeNames.QUERY_INVOKE_NAME);
            return OnQuery(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(Regex commandIdPattern, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), MessageExtensionsInvokeNames.QUERY_INVOKE_NAME);
            return OnQuery(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(RouteSelector routeSelector, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                MessagingExtensionQuery? messagingExtensionQuery;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, MessageExtensionsInvokeNames.QUERY_INVOKE_NAME)
                    || (messagingExtensionQuery = ProtocolJsonSerializer.ToObject<MessagingExtensionQuery>(turnContext.Activity.Value)) == null)
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnQuery() triggered for activity type: {turnContext.Activity.Type}");
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                foreach (MessagingExtensionParameter parameter in messagingExtensionQuery.Parameters)
                {
                    parameters.Add(parameter.Name, parameter.Value);
                }
                Query<IDictionary<string, object>> query = new(messagingExtensionQuery.QueryOptions.Count ?? 25, messagingExtensionQuery.QueryOptions.Skip ?? 0, parameters);
                MessagingExtensionResult result = await handler(turnContext, turnState, query, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    MessagingExtensionActionResponse response = new()
                    {
                        ComposeExtension = result
                    };
                    var activity = Activity.CreateInvokeResponseActivity(response);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(MultipleRouteSelector routeSelectors, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnQuery(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnQuery(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnQuery(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements the logic to handle the tap actions for items returned
        /// by a Search based message extension.
        /// <remarks>
        /// The `composeExtension/selectItem` INVOKE activity does not contain any sort of command ID,
        /// so only a single select item handler can be registered. Developers will need to include a
        /// type name of some sort in the preview item they return if they need to support multiple
        /// select item handlers.
        /// </remarks>>
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSelectItem(SelectItemHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, SELECT_ITEM_INVOKE_NAME));
            };
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                MessagingExtensionResult result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    MessagingExtensionActionResponse response = new()
                    {
                        ComposeExtension = result
                    };
                    var activity = Activity.CreateInvokeResponseActivity(response);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements a Link Unfurling based Message Extension.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQueryLink(QueryLinkHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, MessageExtensionsInvokeNames.QUERY_LINK_INVOKE_NAME));
            };
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                AppBasedLinkQuery? appBasedLinkQuery = ProtocolJsonSerializer.ToObject<AppBasedLinkQuery>(turnContext.Activity.Value);
                MessagingExtensionResult result = await handler(turnContext, turnState, appBasedLinkQuery!.Url, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    MessagingExtensionActionResponse response = new()
                    {
                        ComposeExtension = result
                    };
                    var activity = Activity.CreateInvokeResponseActivity(response);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements the logic to handle anonymous link unfurling.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/anonymousQueryLink` INVOKE activity does not contain any sort of command ID,
        /// so only a single select item handler can be registered.
        /// For more information visit https://learn.microsoft.com/microsoftteams/platform/messaging-extensions/how-to/link-unfurling?#enable-zero-install-link-unfurling
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAnonymousQueryLink(QueryLinkHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, MessageExtensionsInvokeNames.ANONYMOUS_QUERY_LINK_INVOKE_NAME));
            };
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                AppBasedLinkQuery? appBasedLinkQuery = ProtocolJsonSerializer.ToObject<AppBasedLinkQuery>(turnContext.Activity.Value);
                MessagingExtensionResult result = await handler(turnContext, turnState, appBasedLinkQuery!.Url, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    MessagingExtensionActionResponse response = new()
                    {
                        ComposeExtension = result
                    };
                    var activity = Activity.CreateInvokeResponseActivity(response);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that invokes the fetch of the configuration settings for a Message Extension.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/querySettingUrl` INVOKE activity does not contain a command ID, so only a single select item handler can be registered.
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQueryUrlSetting(QueryUrlSettingHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, QUERY_SETTING_URL));
            };
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                MessagingExtensionResult result = await handler(turnContext, turnState, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    MessagingExtensionActionResponse response = new()
                    {
                        ComposeExtension = result
                    };
                    var activity = Activity.CreateInvokeResponseActivity(response);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements the logic to invoke configuring Message Extension settings.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/setting` INVOKE activity does not contain a command ID, so only a single select item handler can be registered.
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnConfigureSettings(ConfigureSettingsHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, CONFIGURE_SETTINGS));
            };
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements the logic when a user has clicked on a button in a Message Extension card.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/onCardButtonClicked` INVOKE activity does not contain any sort of command ID,
        /// so only a single select item handler can be registered. Developers will need to include a
        /// type name of some sort in the preview item they return if they need to support multiple select item handlers.
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnCardButtonClicked(CardButtonClickedHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, QUERY_CARD_BUTTON_CLICKED));
            };
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    var activity = Activity.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        private static RouteSelector CreateTaskSelector(Func<string, bool> isMatch, string invokeName, string? botMessagePreviewAction = default)
        {
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
            {
                bool isInvoke = string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, invokeName);
                if (!isInvoke)
                {
                    return Task.FromResult(false);
                }

                if (turnContext.Activity.Value == null)
                {
                    return Task.FromResult(false);
                }

                var obj = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);

                bool isCommandMatch = obj.TryGetValue("commandId", out JsonElement commandId) && commandId.ValueKind == JsonValueKind.String && isMatch(commandId.ToString());

                bool isPreviewActionMatch = !obj.TryGetValue("botMessagePreviewAction", out JsonElement previewActionToken) 
                    || string.IsNullOrEmpty(previewActionToken.ToString())
                    || string.Equals(botMessagePreviewAction, previewActionToken.ToString());

                return Task.FromResult(isCommandMatch && isPreviewActionMatch);
            };
            return routeSelector;
        }
    }
}
