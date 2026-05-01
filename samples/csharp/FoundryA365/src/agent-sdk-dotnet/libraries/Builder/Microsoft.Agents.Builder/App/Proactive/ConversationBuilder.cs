// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides a builder for constructing instances of the Conversation class with configurable
    /// reference and claims information.
    /// </summary>
    /// <remarks>Use RecordBuilder to incrementally specify details of a conversation reference record, such as
    /// the user, agent, service URL, activity ID, and locale. This class is intended to simplify the creation of
    /// Conversation instances for use in Proactive scenarios. The builder ensures required
    /// fields are set and applies sensible defaults for optional properties if not specified.</remarks>
    public class ConversationBuilder
    {
        private Conversation _conversation = new();

        /// <summary>
        /// Creates a new instance of the ConversationBuilder class with the specified agent client ID, channel ID, and
        /// optional service URL.
        /// </summary>
        /// <param name="agentClientId">The unique identifier for the agent client. Cannot be null or empty.</param>
        /// <param name="channelId">The identifier for the communication channel. Cannot be null or empty.</param>
        /// <param name="serviceUrl">The service URL to associate with the conversation. If null, the default ServiceUrl for
        /// the channel will be used.</param>
        /// <param name="requestorId">The clientId of the app making the request.  Null for Azure Bot Service.</param>
        /// <returns>A ConversationBuilder instance initialized with the specified agent client ID, channel ID, and service URL.</returns>
        public static ConversationBuilder Create(string agentClientId, string channelId, string serviceUrl = null, string requestorId = null)
        {
            var builder = new ConversationBuilder();
            builder._conversation.Reference = ConversationReferenceBuilder.Create(agentClientId, channelId, serviceUrl).Build();
            builder.WithClaimsForClientId(agentClientId, requestorId);
            return builder;
        }

        /// <summary>
        /// Creates a new instance of the ConversationBuilder class initialized with the specified identity, channel ID,
        /// and optional service URL.
        /// </summary>
        /// <param name="identity">The ClaimsIdentity representing the user or bot for whom the conversation is being created. Cannot be null.</param>
        /// <param name="channelId">The identifier of the channel where the conversation will take place. Cannot be null or empty.</param>
        /// <param name="serviceUrl">The service URL to use for the conversation. If null, a default service URL may be used.</param>
        /// <returns>A ConversationBuilder instance configured with the provided identity, channel ID, and service URL.</returns>
        public static ConversationBuilder Create(ClaimsIdentity identity, string channelId, string serviceUrl = null)
        {
            var builder = new ConversationBuilder();
            builder._conversation.Reference = ConversationReferenceBuilder.Create(identity.GetIncomingAudience(), channelId, serviceUrl).Build();
            builder.WithIdentity(identity);
            return builder;
        }

        /// <summary>
        /// Associates a user with the conversation using the specified user ID and optional user name.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to associate with the conversation. Cannot be null, empty, or consist
        /// solely of whitespace.</param>
        /// <param name="userName">The display name of the user. If null, the user will be associated without a display name.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> with the user information set.</returns>
        /// <exception cref="ArgumentException">Thrown when the userId is null, empty, or consists solely of whitespace.</exception>
        public ConversationBuilder WithUser(string userId, string userName = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
            _conversation.Reference.User = new ChannelAccount(userId, userName, RoleTypes.User);
            return this;
        }

        /// <summary>
        /// Associates the specified user with the conversation being built.
        /// </summary>
        /// <param name="user">The user to associate with the conversation. Cannot be null, and the user's Id property must not be null or
        /// whitespace.</param>
        /// <returns>The current instance of the ConversationBuilder with the user set. Enables method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when the ChannelAccount.Id is null, empty, or consists solely of whitespace.</exception>
        public ConversationBuilder WithUser(ChannelAccount user)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(user?.Id, nameof(ChannelAccount.Id));
            _conversation.Reference.User = user;
            return this;
        }

        /// <summary>
        /// Sets the conversation identifier for the builder and associates it with the conversation reference.
        /// </summary>
        /// <param name="conversationId">The unique identifier of the conversation to associate with the builder. Cannot be null, empty, or consist
        /// only of white-space characters.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> with the conversation identifier set.</returns>
        /// <exception cref="ArgumentException">Thrown when the conversationId is null, empty, or consists solely of whitespace.</exception>
        public ConversationBuilder WithConversation(string conversationId)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));
            _conversation.Reference.Conversation = new ConversationAccount(id: conversationId);
            return this;
        }

        /// <summary>
        /// Sets the conversation context for the builder using the specified conversation account.
        /// </summary>
        /// <param name="conversation">The conversation account to associate with the builder. The <paramref name="conversation"/> parameter must
        /// not be null, and its <c>Id</c> property must not be null or whitespace.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> instance with the conversation context set.</returns>
        /// <exception cref="ArgumentException">Thrown when the ConversationAccount.Id is null, empty, or consists solely of whitespace.</exception>
        public ConversationBuilder WithConversation(ConversationAccount conversation)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversation?.Id, nameof(ConversationAccount.Id));
            _conversation.Reference.Conversation = conversation;
            return this;
        }

        /// <summary>
        /// Sets the activity identifier for the reference being built.
        /// </summary>
        /// <param name="activityId">The unique identifier to associate with the activity. Can be null or empty if no activity ID is required.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> instance with the updated activity identifier.</returns>
        public ConversationBuilder WithActivityId(string activityId)
        {
            _conversation.Reference.ActivityId = activityId;
            return this;
        }

        /// <summary>
        /// Adds claims to the record based on the specified client identifier and optional requestor identifier.
        /// </summary>
        /// <remarks>This method replaces any existing claims on the record with a new set containing the
        /// specified client and, if provided, requestor identifiers. Use this method to set claims relevant for
        /// authentication or authorization scenarios.</remarks>
        /// <param name="agentClientId">The client identifier to associate with the 'azp' claim. Cannot be null.</param>
        /// <param name="requestorId">An optional requestor identifier to associate with the 'appid' claim. If null or empty, the 'appid' claim is
        /// not added.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> instance with the updated claims.</returns>
        private ConversationBuilder WithClaimsForClientId(string agentClientId, string requestorId = null)
        {        
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentClientId, nameof(agentClientId));
            var claims = new Dictionary<string, string>
            {
                { "aud", agentClientId },
            };
            if (!string.IsNullOrEmpty(requestorId))
            {
                claims["appid"] = requestorId;
            }
            _conversation = new Conversation(claims, _conversation);
            return this;
        }

        /// <summary>
        /// Sets the claims to associate with the record being built.
        /// </summary>
        /// <param name="claims">A dictionary containing claim types and their corresponding values to assign to the record. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> instance with the specified claims applied.</returns>
        internal ConversationBuilder WithClaims(IDictionary<string, string> claims)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(claims, nameof(claims));
            _conversation = new Conversation(claims, _conversation);
            return this;
        }

        /// <summary>
        /// Sets the identity information for the record using the specified claims identity.
        /// </summary>
        /// <param name="identity">The claims identity to associate with the record. Cannot be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.ConversationBuilder"/> instance with the updated identity information.</returns>
        private ConversationBuilder WithIdentity(ClaimsIdentity identity)
        {
            _conversation = new Conversation(identity, _conversation.Reference);
            return this;
        }

        /// <summary>
        /// Builds and returns the configured conversation reference record.
        /// </summary>
        /// <returns>The constructed <see cref="Microsoft.Agents.Builder.App.Proactive.Conversation"/> instance representing the current state of the
        /// builder.</returns>
        public Conversation Build()
        {
            if (string.IsNullOrWhiteSpace(_conversation.Reference.ServiceUrl))
            {
                _conversation.Reference.ServiceUrl = ConversationReferenceBuilder.ServiceUrlForChannel(_conversation.Reference.ChannelId);
            }

            return _conversation;
        }
    }
}
