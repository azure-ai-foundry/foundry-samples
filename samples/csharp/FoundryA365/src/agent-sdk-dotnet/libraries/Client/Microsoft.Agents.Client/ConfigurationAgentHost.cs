// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Client.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Loads Agent information from configuration.
    /// </summary>
    public class ConfigurationAgentHost : IAgentHost
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnections _connections;
        private readonly IStorage _storage;
        internal IDictionary<string, HttpAgentClientSettings> _agents;

        public ConfigurationAgentHost(
            IServiceProvider systemServiceProvider,
            IStorage storage,
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            IDictionary<string, HttpAgentClientSettings> agents,
            string hostEndpoint,
            string hostClientId)
        {
            _serviceProvider = systemServiceProvider ?? throw new ArgumentNullException(nameof(systemServiceProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));

            if (!string.IsNullOrWhiteSpace(hostEndpoint))
            {
                DefaultResponseEndpoint = new Uri(hostEndpoint);
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentHostMissingProperty, null, nameof(DefaultResponseEndpoint));
            }

            if (!string.IsNullOrWhiteSpace(hostClientId))
            {
                HostClientId = hostClientId;
            }
            else
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentHostMissingProperty, null, nameof(HostClientId));
            }

            LoadAgents(agents);
        }

        /// <summary>
        /// Creates from IConfiguration.
        /// </summary>
        /// <code>
        /// "Agent": {
        ///   "ClientId": "{{ClientId}}",                                  // This is the Client ID used for the remote agent to call you back with.,
        ///   "Description": null,                                         // Optional
        ///   "Publisher": null,                                           // Optional
        ///   "Copyright": null,                                           // Optional
        ///   "Host": {
        ///     "DefaultResponseEndpoint": "http://localhost:3978/api/agentresponse/", // Default host serviceUrl.  Agent can override this via Agents:{{name}}:ConnectionSettings:ServiceUrl
        ///     "Agents": {
        ///       "Echo": {
        ///         "DisplayName": {{optional-displayName}},               // Defaults to node name ("Echo")
        ///         "ConnectionSettings": {
        ///           "ClientId": "{{Agent2ClientId}}",                    // This is the Client ID of the other agent.
        ///           "Endpoint": "http://localhost:39783/api/messages",   // The endpoint of the other agent
        ///           "TokenProvider" : "{{Connections:{{name}}"
        ///         }
        ///       }
        ///     }
        ///   }
        /// }
        /// </code>
        /// <param name="configuration"></param>
        /// <param name="systemServiceProvider"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="configSection"></param>
        public ConfigurationAgentHost(
            IConfiguration configuration,
            IServiceProvider systemServiceProvider,
            IStorage storage, 
            IConnections connections,
            IHttpClientFactory httpClientFactory,
            string configSection = "Agent") : this(
                systemServiceProvider, 
                storage, 
                connections, 
                httpClientFactory,
                configuration?.GetSection($"{configSection}:Host:Agents").Get<IDictionary<string, HttpAgentClientSettings>>(), 
                configuration?.GetValue<string>($"{configSection}:Host:DefaultResponseEndpoint"), 
                configuration?.GetValue<string>($"{configSection}:ClientId"))
        {
        }

        private void LoadAgents(IDictionary<string, HttpAgentClientSettings> agents)
        {
            _agents = agents ?? new Dictionary<string, HttpAgentClientSettings>();
            if (agents != null)
            {
                foreach (var kv in _agents)
                {
                    // If the ServiceUrl isn't supplied at the Agent:ConnectionSettings use
                    // the DefaultHostEndpoint.
                    if (string.IsNullOrEmpty(kv.Value.ConnectionSettings.ServiceUrl))
                    {
                        kv.Value.ConnectionSettings.ServiceUrl = DefaultResponseEndpoint.ToString();
                    }

                    kv.Value.Name = kv.Key;

                    if (string.IsNullOrEmpty(kv.Value.DisplayName))
                    {
                        kv.Value.DisplayName = kv.Value.Name;
                    }

                    kv.Value.ValidateClientSettings();
                }
            }
        }

        /// <inheritdoc />
        public Uri DefaultResponseEndpoint { get; set; }

        /// <inheritdoc />
        public string HostClientId { get; set; }

        /// <inheritdoc/>
        public IAgentClient GetClient(string agentName)
        {
            if (string.IsNullOrWhiteSpace(agentName))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentNotFound, null, "<null>");
            }

            if (!_agents.TryGetValue(agentName, out HttpAgentClientSettings clientSettings))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentNotFound, null, agentName);
            }

            return CreateClient(agentName, clientSettings);
        }

        /// <inheritdoc />
        public IList<IAgentInfo> GetAgents()
        {
            var result = new List<IAgentInfo>();

            foreach (var agent in _agents.Values)
            {
                result.Add(agent);
            }

            return result;
        }

        private static string GetAgentStorageKey(ITurnContext turnContext)
        {
            return $"{turnContext.Activity.Conversation.Id}/agentconversations";
        }

        private static string GetAgentConversationStorageKey(string agentConversationId)
        {
            return agentConversationId;
        }

        /// <inheritdoc/>
        public async Task<string> GetConversation(ITurnContext turnContext, string agentName, CancellationToken cancellationToken = default)
        {
            var key = GetAgentStorageKey(turnContext);
            var items = await _storage.ReadAsync([key], CancellationToken.None);

            if (items != null && items.TryGetValue(key, out var conversations))
            {
                var agentConversations = ProtocolJsonSerializer.ToObject<IDictionary<string, AgentConversation>>(conversations);
                if (agentConversations.TryGetValue(agentName, out var conversation))
                {
                    return conversation.AgentConversationId;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<IList<AgentConversation>> GetConversations(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            var result = new List<AgentConversation>();

            var key = GetAgentStorageKey(turnContext);
            var items = await _storage.ReadAsync([key], CancellationToken.None);

            if (items != null && items.TryGetValue(key, out var conversations))
            {
                var agentConversations = ProtocolJsonSerializer.ToObject<IDictionary<string, AgentConversation>>(conversations);
                foreach (var conversation in agentConversations)
                {
                    result.Add(ProtocolJsonSerializer.ToObject<AgentConversation>(conversation.Value));
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> GetOrCreateConversationAsync(ITurnContext turnContext, string agentName, CancellationToken cancellationToken = default)
        {
            IDictionary<string, AgentConversation> agentConversations = new Dictionary<string, AgentConversation>();
            var items = await _storage.ReadAsync([GetAgentStorageKey(turnContext)], cancellationToken).ConfigureAwait(false);
            if (items != null && items.TryGetValue(GetAgentStorageKey(turnContext), out var conversations))
            {
                agentConversations = ProtocolJsonSerializer.ToObject<IDictionary<string, AgentConversation>>(conversations);

                if (agentConversations.TryGetValue(agentName, out var conversation))
                {
                    return conversation.AgentConversationId;
                }
            }

            // Create the storage key based on the options.
            var conversationReference = turnContext.Activity.GetConversationReference();
            var agentConversationId = Guid.NewGuid().ToString();

            // Create the ChannelConversationReference instance.
            var channelConversationReference = new ChannelConversationReference
            {
                ConversationReference = conversationReference,
                OAuthScope = turnContext.Identity?.GetOutgoingScopes()?.FirstOrDefault(),
                AgentName = agentName,
                AgentConversationId = agentConversationId,
            };

            agentConversations.Add(agentName, new AgentConversation() { AgentConversationId = agentConversationId, AgentName = agentName });

            var changes = new Dictionary<string, object>
            {
                { GetAgentStorageKey(turnContext), agentConversations },
                { GetAgentConversationStorageKey(agentConversationId), channelConversationReference }
            };

            await _storage.WriteAsync(changes, cancellationToken).ConfigureAwait(false);

            return agentConversationId;
        }

        /// <inheritdoc/>
        public async Task DeleteConversationAsync(ITurnContext turnContext, string agentConversationId, CancellationToken cancellationToken = default)
        {
            var deleteKeys = new List<string>
            {
                GetAgentConversationStorageKey(agentConversationId)
            };

            string agentName = null;
            IDictionary<string, AgentConversation> agentConversations;
            var agentConversationsKey = GetAgentStorageKey(turnContext);
            var items = await _storage.ReadAsync([agentConversationsKey], cancellationToken).ConfigureAwait(false);
            if (items != null && items.TryGetValue(agentConversationsKey, out var conversations))
            {
                agentConversations = ProtocolJsonSerializer.ToObject<IDictionary<string, AgentConversation>>(conversations);

                foreach (var conversation in agentConversations)
                {
                    if (conversation.Value.AgentConversationId.Equals(agentConversationId))
                    {
                        agentName = conversation.Value.AgentName;
                        break;
                    }
                }

                if (agentName != null)
                {
                    agentConversations.Remove(agentName);
                    if (agentConversations.Count == 0)
                    {
                        deleteKeys.Add(agentConversationsKey);
                    }
                    else
                    {
                        await _storage.WriteAsync(new Dictionary<string, object>
                        {
                            { agentConversationsKey, agentConversations }
                        }, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            await _storage.DeleteAsync([.. deleteKeys], cancellationToken).ConfigureAwait(false);   
        }

        /// <inheritdoc/>
        public async Task SendToAgent(ITurnContext turnContext, string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var client = GetClient(agentName);
            
            if (string.IsNullOrEmpty(activity.DeliveryMode)
                || !string.Equals(DeliveryModes.Normal, activity.DeliveryMode, StringComparison.OrdinalIgnoreCase))
            {
                activity.DeliveryMode = DeliveryModes.Normal;
            }

            await client.SendActivityAsync<object>(agentConversationId, activity, useAnonymous: AgentClaims.AllowAnonymous(turnContext.Identity), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<object> SendToAgentStreamedAsync(ITurnContext turnContext, string agentName, string agentConversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            using var client = GetClient(agentName);
            return client.SendActivityStreamedAsync(agentConversationId, activity, useAnonymous: AgentClaims.AllowAnonymous(turnContext.Identity), cancellationToken: cancellationToken);
        }

        public async Task EndAgentConversation(ITurnContext turnContext, string agentName, CancellationToken cancellationToken = default)
        {
            var agentConversationId = await GetConversation(turnContext, agentName, cancellationToken).ConfigureAwait(false);

            await DeleteConversationAsync(turnContext, agentConversationId, cancellationToken).ConfigureAwait(false);

            // Send EndOfConversation to the Agent.
            await SendToAgent(turnContext, agentName, agentConversationId, Activity.CreateEndOfConversationActivity(), cancellationToken).ConfigureAwait(false);

        }

        /// <inheritdoc/>
        public async Task EndAllConversations(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            // End all active Agent conversations.
            var deletionKeys = new List<string>
            {
                GetAgentStorageKey(turnContext)
            };

            var activeConversations = await GetConversations(turnContext, cancellationToken).ConfigureAwait(false);
            if (activeConversations.Count > 0)
            {
                foreach (var conversation in activeConversations)
                {
                    deletionKeys.Add(GetAgentConversationStorageKey(conversation.AgentConversationId));

                    // Send EndOfConversation to the Agent.
                    await SendToAgent(turnContext, conversation.AgentName, conversation.AgentConversationId, Activity.CreateEndOfConversationActivity(), cancellationToken).ConfigureAwait(false);
                }
            }

            await _storage.DeleteAsync(deletionKeys.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ChannelConversationReference> GetConversationReferenceAsync(string agentConversationId, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentConversationId, nameof(agentConversationId));

            var channelConversationInfo = await _storage
                .ReadAsync(new[] { GetAgentConversationStorageKey(agentConversationId) }, cancellationToken)
                .ConfigureAwait(false);

            if (channelConversationInfo.TryGetValue(agentConversationId, out var channelConversationReference))
            {
                return ProtocolJsonSerializer.ToObject<ChannelConversationReference>(channelConversationReference);
            }

            return null;
        }

        private HttpAgentClient CreateClient(string agentName, HttpAgentClientSettings clientSettings)
        {
            var tokenProviderName = clientSettings.ConnectionSettings.TokenProvider;
            if (!_connections.TryGetConnection(tokenProviderName, out var tokenProvider))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentTokenProviderNotFound, null, tokenProviderName, agentName);
            }

            return new HttpAgentClient(this, clientSettings, _httpClientFactory, tokenProvider, (ILogger<HttpAgentClient>) _serviceProvider.GetService(typeof(ILogger<HttpAgentClient>)));
        }
    }
}
