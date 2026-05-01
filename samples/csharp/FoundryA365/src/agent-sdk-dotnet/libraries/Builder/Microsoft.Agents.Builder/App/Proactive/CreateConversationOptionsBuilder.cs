// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides a builder for configuring and creating a new conversation with specified conversation parameters.
    /// </summary>
    public class CreateConversationOptionsBuilder
    {
        private readonly CreateConversationOptions _record = new();

        /// <summary>
        /// Creates a new instance of the CreateConversationOptionsBuilder class for initializing a conversation with the
        /// specified agent and channel.
        /// </summary>
        /// <remarks>If the parameters argument is null, default conversation parameters are used. If the
        /// Agent property of parameters is not set, it is initialized with the provided agentClientId.</remarks>
        /// <param name="agentClientId">The unique identifier of the agent client to associate with the conversation. Cannot be null or whitespace.</param>
        /// <param name="channelId">The identifier of the channel where the conversation will take place. Cannot be null or whitespace.</param>
        /// <param name="serviceUrl">The service URL to use for the conversation. If null, a default value may be used.</param>
        /// <param name="parameters">Optional parameters for configuring the conversation. If null, default parameters are used.</param>
        /// <returns>A CreateConversationBuilder instance configured with the specified agent, channel, and parameters.</returns>
        /// <exception cref="ArgumentException">Thrown if required parameters are missing or invalid.</exception>"
        public static CreateConversationOptionsBuilder Create(string agentClientId, ChannelId channelId, string serviceUrl = null, ConversationParameters parameters = null)
        {
            if (string.IsNullOrWhiteSpace(agentClientId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidAgentClientId, null);
            }

            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidChannelId, null);
            }

            var builder = new CreateConversationOptionsBuilder();

            builder._record.Identity = Conversation.IdentityFromClaims(new Dictionary<string, string>
            {
                { "aud", agentClientId }
            });

            builder._record.ChannelId = channelId;
            builder._record.ServiceUrl = serviceUrl;

            builder._record.Parameters = parameters ?? new ConversationParameters();
            if (builder._record.Parameters.Agent == null)
            {
                builder._record.Parameters.Agent = new ChannelAccount(agentClientId);
            }

            return builder;
        }

        /// <summary>
        /// Creates a new instance of the CreateConversationOptionsBuilder class for initializing a conversation with the
        /// specified identity, channel, and parameters.
        /// </summary>
        /// <param name="claims">The ClaimsIdentity.Claims representing the agent or user initiating the conversation. Cannot be null.</param>
        /// <param name="channelId">The identifier of the channel where the conversation will be created. Cannot be null or whitespace.</param>
        /// <param name="serviceUrl">The service URL for the channel. If null, the default service URL is used.</param>
        /// <param name="parameters">Optional parameters for the conversation, such as participants or conversation metadata. If null, default
        /// parameters are used.</param>
        /// <returns>A CreateConversationBuilder instance configured with the specified identity, channel, and parameters.</returns>
        /// <exception cref="ArgumentException">Thrown if required parameters are missing or invalid.</exception>"
        public static CreateConversationOptionsBuilder Create(IDictionary<string, string> claims, ChannelId channelId, string serviceUrl = null, ConversationParameters parameters = null)
        {
            var agentClientId = claims?.FirstOrDefault(c => c.Key == "aud").Value;
            if (string.IsNullOrWhiteSpace(agentClientId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidClaims, null);
            }

            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidChannelId, null);
            }

            var builder = new CreateConversationOptionsBuilder();

            builder._record.Identity = Conversation.IdentityFromClaims(claims);
            builder._record.ChannelId = channelId;
            builder._record.ServiceUrl = serviceUrl;

            builder._record.Parameters = parameters ?? new ConversationParameters();
            if (builder._record.Parameters.Agent == null)
            {
                builder._record.Parameters.Agent = new ChannelAccount(agentClientId);
            }

            return builder;
        }

        /// <summary>
        /// Specifies the user to include as a participant in the conversation being built.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to add to the conversation. Cannot be null.</param>
        /// <param name="userName">The display name of the user to add to the conversation. This value is optional and can be null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> instance with the specified user set as a participant.</returns>
        /// <exception cref="ArgumentException">Thrown if required parameters are missing or invalid.</exception>"
        public CreateConversationOptionsBuilder WithUser(string userId, string userName = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidUserId, null);
            }
            return WithUser(new ChannelAccount(userId.Trim(), userName));
        }

        /// <summary>
        /// Specifies a user to include as a member in the conversation being created.
        /// </summary>
        /// <param name="user">The user account to add as a member of the conversation. Ignored if null.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown if required parameters are missing or invalid.</exception>"
        public CreateConversationOptionsBuilder WithUser(ChannelAccount user)
        {
            if (string.IsNullOrWhiteSpace(user?.Id))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveInvalidUserId, null);
            }

            if (user != null)
            {
                _record.Parameters.Members =
                [
                    user
                ];
            }
            return this;
        }

        /// <summary>
        /// Sets the scope for the conversation being created.
        /// </summary>
        /// <remarks>Use this method to specify a custom scope for the conversation. This does not normally need to be set for Azure Bot Channels.</remarks>
        /// <param name="scope">The scope value to associate with the conversation. If null the default Azure Bot Service scope is used.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> instance with the updated scope.</returns>
        public CreateConversationOptionsBuilder WithScope(string scope)
        {
            _record.Scope = scope;
            return this;
        }

        /// <summary>
        /// Sets the audience for the conversation being created.
        /// </summary>
        /// <remarks>Use this method to specify a custom audience for the conversation. This does not normally need to be set for Azure Bot Channels.</remarks>
        /// <param name="audience">The audience value to associate with the conversation. If null the default Azure Bot Service audience is used.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> instance with the updated audience.</returns>
        public CreateConversationOptionsBuilder WithAudience(string audience)
        {
            _record.Audience = audience;
            return this;
        }

        /// <summary>
        /// Adds an Activity to the conversation being created.  This does not apply to all Channel types, but can be used to specify the initial message 
        /// for those that do.  Teams supports this, and will use the Activity as the initial message in the conversation.  For channels that do not support 
        /// this, the Activity will be ignored.
        /// </summary>
        /// <param name="message">The activity representing the message to include in the conversation. Ignored if null.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> with the specified message added.</returns>
        public CreateConversationOptionsBuilder WithActivity(IActivity message)
        {
            _record.Parameters.Activity = message;
            return this;
        }

        /// <summary>
        /// Sets the channel-specific data for the conversation and returns the updated builder instance.
        /// </summary>
        /// <param name="channelData">The channel-specific data to associate with the conversation. Can be any object required by the channel. May
        /// be null if no channel data is needed.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> with the specified channel data applied.</returns>
        public CreateConversationOptionsBuilder WithChannelData(object channelData)
        {
            SetChannelData(channelData);
            return this;
        }

        /// <summary>
        /// Specifies whether the conversation being created is a group conversation.
        /// </summary>
        /// <param name="isGroup">A value indicating whether the conversation should be treated as a group conversation.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> with the group setting applied.</returns>
        public CreateConversationOptionsBuilder IsGroup(bool isGroup)
        {
            _record.Parameters.IsGroup = isGroup;
            return this;
        }

        /// <summary>
        /// Sets the topic name for the conversation being created.
        /// </summary>
        /// <remarks>Use this method to specify a topic for the conversation before finalizing its
        /// creation. Calling this method multiple times will overwrite the previously set topic name.</remarks>
        /// <param name="topicName">The name of the topic to associate with the conversation. Ignored if null or empty.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> with the updated topic name.</returns>
        public CreateConversationOptionsBuilder WithTopicName(string topicName)
        {
            _record.Parameters.TopicName = topicName?.Trim();
            return this;
        }

        /// <summary>
        /// Sets the tenant identifier for the conversation being created and returns the builder instance for method
        /// chaining.
        /// </summary>
        /// <param name="tenantId">The unique identifier of the tenant to associate with the conversation. Ignored if null or empty.</param>
        /// <returns>The current <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> instance with the specified tenant identifier applied.</returns>
        public CreateConversationOptionsBuilder WithTenantId(string tenantId)
        {
            _record.Parameters.TenantId = tenantId?.Trim();

            if (_record.ChannelId == Channels.Msteams && !string.IsNullOrEmpty(_record.Parameters.TenantId))
            {
                SetChannelData(new
                {
                    tenant = new
                    {
                        id = _record.Parameters.TenantId
                    }
                });
            }

            return this;
        }

        /// <summary>
        /// Specifies the Microsoft Teams channel ID to associate with the conversation being built.
        /// </summary>
        /// <remarks>This method only applies the Teams channel. For other channels, this method has no effect. Note that
        /// this "channel id" is not the same as the Activity.ChannelId values.<br/><br/>
        /// More information about teams information can be acquired by using the 
        /// <see href="https://learn.microsoft.com/en-us/MicrosoftTeams/teams-powershell-overview">Teams PowerShell Overview</see>.
        /// </remarks>
        /// <param name="teamsChannelId">The unique identifier of the Microsoft Teams channel to set for the conversation. If null or empty this has no effect.</param>
        /// <returns>The current instance of <see cref="Microsoft.Agents.Builder.App.Proactive.CreateConversationOptionsBuilder"/> to allow method chaining.</returns>
        public CreateConversationOptionsBuilder WithTeamsChannelId(string teamsChannelId)
        {
            if (_record.ChannelId == Channels.Msteams && !string.IsNullOrWhiteSpace(teamsChannelId))
            {
                IsGroup(true);
                SetChannelData(new
                {
                    channel = new
                    {
                        id = teamsChannelId.Trim()
                    }
                });
            }

            return this;
        }

        /// <summary>
        /// Specifies whether the conversation should be stored for future retrieval.
        /// </summary>
        /// <param name="store">true to store the conversation; otherwise, false.</param>
        /// <returns>The current instance of <see cref="CreateConversationOptionsBuilder"/> for method chaining.</returns>
        public CreateConversationOptionsBuilder WithStoreConversation(bool store)
        {
            _record.StoreConversation = store;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured instance of the CreateConversation object.
        /// </summary>
        /// <returns>A CreateConversation instance containing the configured create conversation parameters.</returns>
        /// <exception cref="ArgumentException">Thrown if required parameters are missing or invalid.</exception>"
        public CreateConversationOptions Build()
        {
            if (_record.Parameters.Members?.Count == 0)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.ProactiveMissingMembers, null);
            }

            if (string.IsNullOrWhiteSpace(_record.ServiceUrl))
            {
                _record.ServiceUrl = ConversationReferenceBuilder.ServiceUrlForChannel(_record.ChannelId);
            }

            if (string.IsNullOrWhiteSpace(_record.Scope))
            {
                _record.Scope = CreateConversationOptions.AzureBotScope;
            }

            if (string.IsNullOrWhiteSpace(_record.Audience))
            {
                _record.Audience = CreateConversationOptions.AzureBotAudience;
            }

            if (_record.Parameters.Activity != null && string.IsNullOrWhiteSpace(_record.Parameters.Activity.Type))
            {
                _record.Parameters.Activity.Type = ActivityTypes.Message;
            }

            return _record;
        }

        private void SetChannelData(object channelData)
        {
            if (_record.Parameters.ChannelData == null)
            {
                _record.Parameters.ChannelData = channelData;
            }
            else
            {
                _record.Parameters.ChannelData = ObjectPath.Merge<object>(_record.Parameters.ChannelData, channelData);
            }
        }
    }
}
