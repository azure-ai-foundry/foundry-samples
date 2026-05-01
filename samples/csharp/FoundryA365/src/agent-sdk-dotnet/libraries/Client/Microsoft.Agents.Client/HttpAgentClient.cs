// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Client.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Sends Activities to a remote Agent.
    /// </summary>
    internal class HttpAgentClient : IAgentClient
    {
        private readonly HttpAgentClientSettings _settings;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HttpAgentClient> _logger;
        private readonly IAgentHost _agentHost;
        private bool _disposed;

        /// <param name="agentHost"></param>
        /// <param name="clientSettings"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="tokenProvider"></param>
        /// <param name="logger"></param>
        public HttpAgentClient(
            IAgentHost agentHost,
            HttpAgentClientSettings clientSettings,
            IHttpClientFactory httpClientFactory,
            IAccessTokenProvider tokenProvider,
            ILogger<HttpAgentClient> logger = null)
        {
            _settings = clientSettings ?? throw new ArgumentNullException(nameof(clientSettings));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? NullLogger<HttpAgentClient>.Instance;
            _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));
        }

        /// <inheritdoc/>
        public string Name => _settings.Name;

        /// <inheritdoc/>
        public async Task SendActivityAsync(string agentConversationId, IActivity activity, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            await SendActivityAsync<object>(agentConversationId, activity, relatesTo, useAnonymous, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<InvokeResponse<T>> SendActivityAsync<T>(string agentConversationId, IActivity activity, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentConversationId, nameof(agentConversationId));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation("SendActivityAsync: RequestId={RequestId}, AgentId={AgentClientId}, Endpoint={AgentEndpoint}, Activity={Activity}", 
                    activity.RequestId, _settings.ConnectionSettings.ClientId, _settings.ConnectionSettings.Endpoint.ToString(), ProtocolJsonSerializer.ToJson(activity));
            }

            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = CreateSendActivity(agentConversationId, activity, relatesTo);

            // Create the HTTP request from the cloned Activity and send it to the Agent.
            using var response = await SendRequest(activityClone, useAnonymous, cancellationToken).ConfigureAwait(false);
#if !NETSTANDARD
            var content = response.Content != null ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : null;
#else
            var content = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;
#endif

            // On success assuming either JSON that can be deserialized to T or empty.
            return new InvokeResponse<T>
            {
                Status = (int)response.StatusCode,
                Body = content?.Length > 0 ? ProtocolJsonSerializer.ToObject<T>(content) : default
            };
        }

        /// <inheritdoc/>
        public async Task<StreamResponse<T>> SendActivityStreamedAsync<T>(string agentConversationId, IActivity activity, Action<IActivity> handler, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(agentConversationId, nameof(agentConversationId));
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            await foreach (var received in SendActivityStreamedAsync(agentConversationId, activity, relatesTo, useAnonymous, cancellationToken))
            {
                if (received is IActivity receivedActivity)
                {
                    if (receivedActivity.Type == ActivityTypes.EndOfConversation)
                    {
                        if (!string.IsNullOrEmpty(receivedActivity.Code) && receivedActivity.Code != EndOfConversationCodes.CompletedSuccessfully)
                        {
                            return new StreamResponse<T>()
                            {
                                Status = StreamResponseStatus.Error,
                                Error = $"eoc:{receivedActivity.Code}",
                            };
                        }

                        return new StreamResponse<T>()
                        {
                            Status = StreamResponseStatus.Complete,
                            Value = ProtocolJsonSerializer.ToObject<T>(receivedActivity.Value)
                        };
                    }

                    handler(receivedActivity);
                }
                else if (received is InvokeResponse invokeResponse)
                {
                    if (invokeResponse.Status >= 200 && invokeResponse.Status <= 299)
                    {
                        return new StreamResponse<T>()
                        {
                            Status = StreamResponseStatus.Error,
                            Error = $"invoke:{invokeResponse.Status}",
                        };
                    }

                    if (activity.DeliveryMode == DeliveryModes.ExpectReplies)
                    {
                        var expectedReplies = ProtocolJsonSerializer.ToObject<ExpectedReplies>(invokeResponse.Body);
                        foreach (var reply in expectedReplies.Activities)
                        {
                            handler(reply);
                        }

                        return new StreamResponse<T>()
                        {
                            Status = StreamResponseStatus.Complete,
                            Value = ProtocolJsonSerializer.ToObject<T>(expectedReplies.Body)
                        };
                    }
                    else
                    {
                        return new StreamResponse<T>()
                        {
                            Status = StreamResponseStatus.Complete,
                            Value = ProtocolJsonSerializer.ToObject<T>(invokeResponse.Body)
                        };
                    }
                }
            }

            return new StreamResponse<T>()
            {
                Status = StreamResponseStatus.Pending
            };
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<object> SendActivityStreamedAsync(string agentConversationId, IActivity activity, IActivity relatesTo = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            var activityClone = CreateSendActivity(agentConversationId, activity, relatesTo);
            activityClone.DeliveryMode = DeliveryModes.Stream;

            return InnerSendActivityStreamedAsync(activityClone, useAnonymous, cancellationToken);
        }

        public async IAsyncEnumerable<object> InnerSendActivityStreamedAsync(IActivity activity, bool useAnonymous = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogInformation("SendActivityStreamedAsync: RequestId={RequestId}, AgentId={AgentClientId}, Endpoint={AgentEndpoint}, Activity={Activity}",
                    activity.RequestId, _settings.ConnectionSettings.ClientId, _settings.ConnectionSettings.Endpoint.ToString(), ProtocolJsonSerializer.ToJson(activity));
            }

            // Create the HTTP request from the cloned Activity and send it to the Agent.
            using var response = await SendRequest(activity, useAnonymous, cancellationToken).ConfigureAwait(false);

            // Read streamed response
#if !NETSTANDARD
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
            using Stream stream = await response.Content.ReadAsStreamAsync();
#endif
            using StreamReader sr = new(stream);
            string streamType = string.Empty;

            string line;
            while ((line = ReadLineSafe(sr)) != null)
            {
                if (line!.StartsWith("event:", StringComparison.InvariantCulture))
                {
                    streamType = line[7..];
                }
                else if (line.StartsWith("data:", StringComparison.InvariantCulture) && streamType == "activity")
                {
                    string jsonRaw = line[6..];
                    var inActivity = ProtocolJsonSerializer.ToObject<IActivity>(jsonRaw);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Stream Response: RequestId={RequestId}, Activity='{Activity}'", activity.RequestId, jsonRaw);
                    }

                    yield return inActivity;
                }
                else if (line.StartsWith("data:", StringComparison.InvariantCulture) && streamType == "invokeResponse")
                {
                    string jsonRaw = line[6..];

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Stream Response: RequestId={RequestId}, InvokeResponse='{InvokeResponse}'", activity.RequestId, jsonRaw);
                    }

                    yield return ProtocolJsonSerializer.ToObject<InvokeResponse>(jsonRaw);
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogWarning("AgentClient {AgentName}: Unexpected stream data {StreamType}, {LineValue}", Name, streamType, line.Trim());
                }
            }
        }

        private static string ReadLineSafe(StreamReader reader)
        {
            try
            {
                return reader.ReadLine();
            }
            catch (Exception)
            {
                // TBD:  Not sure how to resolve this yet.  It is because Readline will throw when the 
                // other end closes the stream.
                // (HttpIoException.HttpRequestError == HttpRequestError.ResponseEnded)
                return null;
            }
        }

        private async Task<HttpResponseMessage> SendRequest(IActivity activity, bool useAnonymous, CancellationToken cancellationToken)
        {
            var jsonContent = new StringContent(activity.ToJson(), Encoding.UTF8, "application/json");
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = _settings.ConnectionSettings.Endpoint,
                Content = jsonContent
            };

            using var httpClient = _httpClientFactory.CreateClient(nameof(HttpAgentClient));

            httpClient.AddHeaderPropagation();

            // Add the auth header to the HTTP request.
            if (!useAnonymous)
            {
                var tokenResult = await _tokenProvider.GetAccessTokenAsync(_settings.ConnectionSettings.ResourceUrl, [$"{_settings.ConnectionSettings.ClientId}/.default"]).ConfigureAwait(false);
                httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult);
            }

            var completionOption = activity.DeliveryMode == DeliveryModes.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            HttpResponseMessage response;

            try
            {
                response = await httpClient.SendAsync(httpRequestMessage, completionOption, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendActivityAsync: Channel request failed to '{ChannelName}' at '{ChannelEndpoint}'", Name, _settings.ConnectionSettings.Endpoint.ToString());
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.SendToAgentFailed, ex, Name);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SendActivityAsync: Channel request unsuccessful to '{ChannelName}' at '{ChannelEndpoint}' returned '{ChannelResponse}'", 
                    Name, _settings.ConnectionSettings.Endpoint.ToString(), (int)response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.SendToAgentUnauthorized, null, Name);
                }
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.SendToAgentUnsuccessful, null, Name, response.StatusCode.ToString());
            }

            return response;
        }

        private IActivity CreateSendActivity(string agentConversationId, IActivity activity, IActivity relatesTo)
        {
            // Clone the activity so we can modify it before sending without impacting the original object.
            var activityClone = activity.Clone();

            // Apply the appropriate addressing to the newly created Activity.
            if (relatesTo != null)
            {
                activityClone.RelatesTo = new ConversationReference
                {
                    ServiceUrl = relatesTo.ServiceUrl,
                    ActivityId = relatesTo.Id,
                    ChannelId = relatesTo.ChannelId,
                    Locale = relatesTo.Locale,
                    Conversation = new ConversationAccount
                    {
                        Id = relatesTo.Conversation.Id,
                        Name = relatesTo.Conversation.Name,
                        ConversationType = relatesTo.Conversation.ConversationType,
                        AadObjectId = relatesTo.Conversation.AadObjectId,
                        IsGroup = relatesTo.Conversation.IsGroup,
                        Properties = relatesTo.Conversation.Properties,
                        Role = relatesTo.Conversation.Role,
                        TenantId = relatesTo.Conversation.TenantId,
                    }
                };
            }

            activityClone.RequestId = Guid.NewGuid().ToString();
            activityClone.ServiceUrl = _settings.ConnectionSettings.ServiceUrl;
            activityClone.Recipient ??= new ChannelAccount();
            activityClone.Recipient.Role = RoleTypes.Skill;
            activityClone.From ??= new ChannelAccount();
            activityClone.From.Role = RoleTypes.Agent;

            activityClone.Conversation ??= new ConversationAccount();
            if (!string.IsNullOrEmpty(activityClone.Conversation.Id))
            {
                activityClone.Conversation.Id = agentConversationId;
            }

            return activityClone;
        }

        private Activity CreateConversationUpdateActivity(ITurnContext turnContext, string agentConversationId, bool streamed)
        {
            return new Activity()
            {
                Type = ActivityTypes.ConversationUpdate,
                Id = Guid.NewGuid().ToString(),
                ChannelId = turnContext.Activity.ChannelId,
                DeliveryMode = streamed ? DeliveryModes.Stream : DeliveryModes.Normal,
                Conversation = new ConversationAccount() { Id = agentConversationId },
                MembersAdded = [new ChannelAccount() { Id = _agentHost.HostClientId, Role = RoleTypes.Agent }, new ChannelAccount() { Id = _settings.ConnectionSettings.ClientId, Role = RoleTypes.Skill }],
                ServiceUrl = _settings.ConnectionSettings.ServiceUrl,
                Recipient = new ChannelAccount() { Id = _settings.ConnectionSettings.ClientId, Role = RoleTypes.Skill },
                From = new ChannelAccount() { Id = _agentHost.HostClientId, Role = RoleTypes.Agent },
            };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of dispose pattern.
        /// </summary>
        /// <param name="disposing">Indicates where this method is called from.</param>
        protected void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}
