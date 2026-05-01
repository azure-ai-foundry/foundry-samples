// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing Teams ConversationUpdate activities in an AgentApplication.
    /// </summary>
    /// <remarks>Use <see cref="Microsoft.Agents.Extensions.Teams.App.Builders.TeamsConversationUpdateRouteBuilder"/> to create and configure routes that respond to conversation
    /// update activities. This builder allows matching update activities by name, and supports agentic routing scenarios.</remarks>
    public class TeamsConversationUpdateRouteBuilder : RouteBuilderBase<TeamsConversationUpdateRouteBuilder>
    {
        /// <summary>
        /// Configures the route to match a specific <see cref="Microsoft.Agents.Extensions.Teams.App.TeamsConversationUpdateEvents"/>, such as members being added or removed.
        /// </summary>
        /// <remarks>Use this method to restrict the route to trigger only for a particular conversation
        /// update event. If the specified event is not recognized, the route will match any conversation update
        /// activity.</remarks>
        /// <param name="eventName">The name of the conversation update event to match. Common values include events for members being added or
        /// removed. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.ConversationUpdateRouteBuilder"/> instance for method chaining.</returns>
        public TeamsConversationUpdateRouteBuilder WithUpdateEvent(string eventName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(eventName, nameof(eventName));

            switch (eventName)
            {
                case TeamsConversationUpdateEvents.ChannelCreated:
                case TeamsConversationUpdateEvents.ChannelDeleted:
                case TeamsConversationUpdateEvents.ChannelRenamed:
                case TeamsConversationUpdateEvents.ChannelRestored:
                case TeamsConversationUpdateEvents.ChannelShared:
                case TeamsConversationUpdateEvents.ChannelUnshared:
                    {
                        _route.Selector = (context, _) =>
                        {
                            var teamChannelData = context.Activity.GetChannelData<TeamsChannelData>();
                            return Task.FromResult
                            (
                                string.Equals(context.Activity.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                                && _route.IsChannelIdMatch(context.Activity.ChannelId)
                                && string.Equals(teamChannelData?.EventType, eventName)
                                && teamChannelData?.Channel != null
                                && teamChannelData?.Team != null
                            );
                        };
                        break;
                    }
                case TeamsConversationUpdateEvents.MembersAdded:
                    {
                        _route.Selector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && context.Activity?.MembersAdded != null
                            && context.Activity.MembersAdded.Count > 0
                        );
                        break;
                    }
                case TeamsConversationUpdateEvents.MembersRemoved:
                    {
                        _route.Selector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && context.Activity?.MembersRemoved != null
                            && context.Activity.MembersRemoved.Count > 0
                        );
                        break;
                    }
                case TeamsConversationUpdateEvents.TeamRenamed:
                case TeamsConversationUpdateEvents.TeamDeleted:
                case TeamsConversationUpdateEvents.TeamHardDeleted:
                case TeamsConversationUpdateEvents.TeamArchived:
                case TeamsConversationUpdateEvents.TeamUnarchived:
                case TeamsConversationUpdateEvents.TeamRestored:
                    {
                        _route.Selector = (context, _) =>
                        {
                            var teamChannelData = context.Activity.GetChannelData<TeamsChannelData>();
                            return Task.FromResult
                            (
                                string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                                && _route.IsChannelIdMatch(context.Activity.ChannelId)
                                && string.Equals(teamChannelData?.EventType, eventName)
                                && teamChannelData?.Team != null
                            );
                        };
                        break;
                    }
                default:
                    {
                        _route.Selector = (context, _) =>
                        {
                            var teamChannelData = context.Activity.GetChannelData<TeamsChannelData>();
                            return Task.FromResult
                            (
                                string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                                && _route.IsChannelIdMatch(context.Activity.ChannelId)
                                && string.Equals(teamChannelData?.EventType, eventName)
                            );
                        };
                        break;
                    }
            }
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public TeamsConversationUpdateRouteBuilder WithHandler(RouteHandler handler)
        {
            _route.Handler = handler;
            return this;
        }

        /// <summary>
        /// Returns the current event route builder instance. For event routes, the invoke flag is ignored to
        /// prevent misconfiguration.
        /// </summary>
        /// <remarks>Conversation updates cannot be configured as invoke routes. This method always returns the
        /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Extensions.Teams.App.Builders.TeamsConversationUpdateRouteBuilder"/>.</returns>
        public new TeamsConversationUpdateRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
