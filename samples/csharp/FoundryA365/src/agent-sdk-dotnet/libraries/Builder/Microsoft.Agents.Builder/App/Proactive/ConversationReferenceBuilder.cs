// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides a fluent builder for constructing a ConversationReference with configurable channel, conversation,
    /// user, agent, and related properties.
    /// </summary>
    /// <remarks>Use ReferenceBuilder to incrementally specify details of a conversation reference, such as
    /// the user, agent, service URL, activity ID, and locale. This class is intended to simplify the creation of
    /// ConversationReference instances for use Proactive scenarios. The builder ensures required
    /// fields are set and applies sensible defaults for optional properties if not specified.</remarks>
    public class ConversationReferenceBuilder
    {
        private readonly ConversationReference _reference = new();

        /// <summary>
        /// Creates a new instance of the ReferenceBuilder class initialized with the specified channel and conversation
        /// identifiers.
        /// </summary>
        /// <param name="channelId">The unique identifier of the channel in which the conversation takes place. Cannot be null or empty.</param>
        /// <param name="conversationId">The unique identifier of the conversation within the channel. Cannot be null or empty.</param>
        /// <returns>A ReferenceBuilder instance initialized with the provided channel and conversation identifiers.</returns>
        public static ConversationReferenceBuilder Create(ChannelId channelId, string conversationId)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

            var builder = new ConversationReferenceBuilder();
            builder._reference.ChannelId = channelId;
            builder._reference.Conversation = new ConversationAccount(id: conversationId);
            return builder;
        }

        /// <summary>
        /// Creates a new instance of the ReferenceBuilder class initialized with the specified agent ID, channel ID,
        /// and optional service URL.
        /// </summary>
        /// <param name="agentClientId">The unique identifier of the agent. Cannot be null or empty.</param>
        /// <param name="channelId">The identifier of the channel to associate with the reference. Cannot be null or empty.</param>
        /// <param name="serviceUrl">The service URL to associate with the reference, or null to omit the service URL.</param>
        /// <returns>A ReferenceBuilder instance initialized with the provided agent ID, channel ID, and optional service URL.</returns>
        public static ConversationReferenceBuilder Create(string agentClientId, ChannelId channelId, string serviceUrl = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, nameof(agentClientId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));
            var builder = new ConversationReferenceBuilder();
            builder._reference.Agent = AgentForChannel(channelId, agentClientId);
            builder._reference.ChannelId = channelId;
            builder._reference.ServiceUrl = serviceUrl;
            return builder;
        }

        /// <summary>
        /// Sets the user information for the reference using the specified user ID, optional user name, and role.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to associate with the reference. Cannot be null.</param>
        /// <param name="userName">The display name of the user. May be null if no display name is available.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder"/> instance with the updated user information.</returns>
        public ConversationReferenceBuilder WithUser(string userId, string? userName = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
            _reference.User = new ChannelAccount(userId, userName, RoleTypes.User);
            return this;
        }

        /// <summary>
        /// Sets the user associated with the conversation reference being built.
        /// </summary>
        /// <param name="user">The user to associate with the conversation reference. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder"/> instance for method chaining.</returns>
        public ConversationReferenceBuilder WithUser(ChannelAccount user)
        {
            _reference.User = user;
            return this;
        }

        /// <summary>
        /// Sets the agent information for the reference using the specified agent ID, name, and role.
        /// </summary>
        /// <param name="agentClientId">The unique identifier of the agent. Cannot be null.</param>
        /// <param name="agentName">Optional Agent name.</param>
        /// <returns>The current ReferenceBuilder instance with the updated agent information.</returns>
        public ConversationReferenceBuilder WithAgent(string agentClientId, string agentName = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, nameof(agentClientId));
            _reference.Agent = AgentForChannel(_reference.ChannelId, agentClientId, agentName);
            return this;
        }

        /// <summary>
        /// Sets the agent information for the reference and returns the current builder instance.
        /// </summary>
        /// <param name="agent">The agent to associate with the reference. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder"/> instance with the updated agent information.</returns>
        public ConversationReferenceBuilder WithAgent(ChannelAccount agent)
        {
            _reference.Agent = agent;
            return this;
        }

        /// <summary>
        /// Sets the service URL for the reference and returns the current builder instance.
        /// </summary>
        /// <param name="serviceUrl">The service URL to associate with the reference. If null, a default will be provided in <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder.Build"/>.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder"/> instance with the updated service URL.</returns>
        public ConversationReferenceBuilder WithServiceUrl(string serviceUrl)
        {
            _reference.ServiceUrl = serviceUrl;
            return this;
        }

        /// <summary>
        /// Sets the activity identifier for the reference being built.
        /// </summary>
        /// <param name="activityId">The unique identifier to associate with the activity. Can be null or empty if no activity ID is required.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder"/> instance with the updated activity identifier.</returns>
        public ConversationReferenceBuilder WithActivityId(string activityId)
        {
            _reference.ActivityId = activityId;
            return this;
        }

        /// <summary>
        /// Sets the locale for the reference being built.
        /// </summary>
        /// <param name="locale">The locale identifier to assign to the reference. This should be a valid IETF language tag (for example,
        /// "en-US" or "fr-FR").</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationReferenceBuilder"/> instance with the updated locale.</returns>
        public ConversationReferenceBuilder WithLocale(string locale)
        {
            _reference.Locale = locale;
            return this;
        }

        /// <summary>
        /// Builds and returns a complete conversation reference, ensuring all required fields are populated.
        /// </summary>
        /// <remarks>If the <c>ServiceUrl</c> property is not set, it is automatically determined based on
        /// the channel ID. The <c>Agent</c> and <c>User</c> properties are initialized with default roles if they are
        /// null.</remarks>
        /// <returns>A <see cref="Microsoft.Agents.Core.Models.ConversationReference"/> instance with guaranteed non-null <c>ServiceUrl</c>, <c>Agent</c>, and
        /// <c>User</c> properties.</returns>
        public ConversationReference Build()
        {
            if (string.IsNullOrEmpty(_reference.ServiceUrl))
            {
                _reference.ServiceUrl = ConversationReferenceBuilder.ServiceUrlForChannel(_reference.ChannelId);
            }

            _reference.Agent ??= new ChannelAccount(role: RoleTypes.Agent);
            _reference.User ??= new ChannelAccount(role: RoleTypes.User);

            return _reference;
        }

        public static string ServiceUrlForChannel(ChannelId channelId)
        {
            return channelId.Channel switch
            {
                Channels.Msteams => "https://smba.trafficmanager.net/teams/",
                _ => $"https://{channelId.Channel}.botframework.com/"
            };
        }

        private static ChannelAccount AgentForChannel(ChannelId channelId, string agentClientId, string agentName = null)
        {
            return channelId == Channels.Msteams
                ? new ChannelAccount($"28:{agentClientId}", agentName, role: RoleTypes.Agent)
                : new ChannelAccount(agentClientId, agentName, role: RoleTypes.Agent);
        }
    }
}
