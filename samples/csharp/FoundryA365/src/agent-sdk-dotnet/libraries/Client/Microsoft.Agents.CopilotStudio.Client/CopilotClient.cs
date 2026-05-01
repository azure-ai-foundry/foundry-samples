// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.CopilotStudio.Client.Interfaces;
using Microsoft.Agents.CopilotStudio.Client.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Microsoft.Agents.CopilotStudio.Client.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// This client is used to connect to the Direct-to-Engine API endpoint for Copilot Studio.
    /// </summary>
    public class CopilotClient : ICopilotClient
    {
        /// <summary>
        /// The conversation ID being used for the current conversation.
        /// </summary>
        private string _conversationId = string.Empty;
        /// <summary>
        /// The content type for event stream.
        /// </summary>
        private static readonly MediaTypeWithQualityHeaderValue s_EventStream = new("text/event-stream");
        /// <summary>
        /// The content type for conversation start.
        /// </summary>
        private static readonly MediaTypeWithQualityHeaderValue s_ApplicationJson = new("application/json");
        /// <summary>
        /// The HTTP client factory to use for connecting to Copilot Studio.
        /// </summary>
        private IHttpClientFactory _httpClientFactory;
        /// <summary>
        /// The HTTP client name to use from the factory.
        /// </summary>
        private readonly string _httpClientName = string.Empty;
        /// <summary>
        /// The logger for Direct-to-Engine operations.
        /// </summary>
        private readonly ILogger _logger;
        /// <summary>
        /// The token provider function to get the access token for the request.
        /// </summary>
        private readonly Func<string, Task<string>>? _tokenProviderFunction = null;
        /// <summary>
        /// The island experimental URL for Copilot Studio.
        /// </summary>
        private string _IslandExperimentalUrl = string.Empty;
        /// <summary>
        /// The connection settings for Copilot Studio.
        /// </summary>
        public ConnectionSettings Settings;


        /// <summary>
        /// Returns the scope URL needed to connect to Copilot Studio from the connection settings.
        /// </summary>
        /// <param name="settings">The Copilot Studio connection settings.</param>
        /// <returns>The token audience scope URL as a string.</returns>
        public static string ScopeFromSettings(ConnectionSettings settings) => PowerPlatformEnvironment.GetTokenAudience(settings);

        /// <summary>
        /// Returns the scope URL needed to connect to Copilot Studio from the Power Platform cloud.
        /// </summary>
        /// <param name="cloud">The Power Platform cloud to use.</param>
        /// <returns>The token audience scope URL as a string, or null if not available.</returns>
        public static string? ScopeFromCloud(PowerPlatformCloud cloud) => PowerPlatformEnvironment.GetTokenAudience(null, cloud);

        /// <summary>
        /// Creates a Direct-to-Engine client for Microsoft Copilot Studio hosted bots.
        /// </summary>
        /// <param name="settings">The configuration settings for connecting to Copilot Studio.</param>
        /// <param name="httpClientFactory">The HTTP client factory to use when connecting to Copilot Studio.</param>
        /// <param name="httpClientName">The name of the HTTP client to use from the factory.</param>
        /// <param name="logger">The logger for Direct-to-Engine operations.</param>
        public CopilotClient(ConnectionSettings settings, IHttpClientFactory httpClientFactory, ILogger logger, string httpClientName = "mcs")
        {
            Settings = settings;
            _httpClientFactory = httpClientFactory;
            _httpClientName = httpClientName;
            _logger = logger;
        }


        /// <summary>
        /// Creates a Direct-to-Engine client for Microsoft Copilot Studio hosted agents.
        /// </summary>
        /// <param name="settings">The configuration settings for connecting to Copilot Studio.</param>
        /// <param name="httpClientFactory">The HTTP client factory to use when connecting to Copilot Studio.</param>
        /// <param name="tokenProviderFunction">The function pointer for an async function that will accept a URL and return an access token.</param>
        /// <param name="logger">The logger for Direct-to-Engine operations.</param>
        /// <param name="httpClientName">The name of the HTTP client to use from the factory.</param>
        public CopilotClient(ConnectionSettings settings, IHttpClientFactory httpClientFactory, Func<string, Task<string>> tokenProviderFunction, ILogger logger, string httpClientName)
        {
            Settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _httpClientName = httpClientName;
            _tokenProviderFunction = tokenProviderFunction;
        }

        #region Start Conversation Overloads

        /// <inheritdoc/>
        public async IAsyncEnumerable<IActivity> StartConversationAsync(bool emitStartConversationEvent = true, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StartRequest req = new() { EmitStartConversationEvent = emitStartConversationEvent };
            using (_logger.BeginScope("D2E:StartConversationAsync"))
            {
                await foreach (var result in StartConversationAsync(req, cancellationToken))
                {
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IActivity> StartConversationAsync(StartRequest startRequest, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using (_logger.BeginScope("D2E:StartConversationAsync"))
            {
                _logger.LogTrace("Starting conversation");

                _ = startRequest ?? throw new ArgumentNullException(nameof(startRequest));

                Uri uriStart = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(Settings, null);
                HttpRequestMessage req = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = uriStart,
                    Headers =
                    {
                        Accept = { s_EventStream }
                    },
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(startRequest)))
                    {
                        Headers =
                        {
                            ContentType = s_ApplicationJson,
                        }
                    }
                };
                req.Headers.UserAgent.ParseAdd(UserAgentHelper.UserAgentHeader);
                if (!string.IsNullOrEmpty(startRequest.ConversationId))
                {
                    req.Headers.Add(CopilotStudioHeaderNames.ConversationId, startRequest.ConversationId);
                }
                await foreach (var activity in PostActivityRequestAsync(req, RequestTypes.StartSession, cancellationToken))
                {
                    yield return activity;
                }
            }
        }

        #endregion

        #region SendActivity Overloads
        /// <inheritdoc/>
        public IAsyncEnumerable<IActivity> AskQuestionAsync(string question, string? conversationId = default, CancellationToken cancellationToken = default)
        {
            using (_logger.BeginScope("D2E:AskQuestionAsync"))
            {
                var activity = new Activity
                {
                    Type = "message",
                    Text = question,
                    Conversation = new ConversationAccount { Id = conversationId }
                };
                return SendActivityAsync(activity, cancellationToken);
            }
        }


        /// <inheritdoc/>
        public async IAsyncEnumerable<IActivity> ExecuteAsync(string conversationId, IActivity activityToSend, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _ = activityToSend ?? throw new ArgumentNullException(nameof(activityToSend), "An Activity is required to use this method.");
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId), "A valid Conversation Id is required to use this method.");

            using (_logger.BeginScope("D2E:ExecuteAsync"))
            {
                // Force conversation ID to be the one provided in the parameter, this is to avoid any confusion on which conversation ID is being used as the reference for this method.
                if (activityToSend.Conversation is null)
                {
                    activityToSend.Conversation = new ConversationAccount { Id = conversationId };
                }
                else
                {
                    activityToSend.Conversation.Id = conversationId;
                }

                await foreach (var activity in ExecuteRequestAction(activityToSend, cancellationToken))
                {
                    yield return activity;
                }
            }
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<IActivity> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            _ = activity ?? throw new ArgumentNullException(nameof(activity), "An Activity is required to use this method.");
            using (_logger.BeginScope("D2E:SendActivityAsync"))
            {
                return ExecuteRequestAction(activity, cancellationToken);
            }
        }


        /// <inheritdoc/>
        [Obsolete("AskQuestionAsync(IActivity, CancellationToken) is deprecated. Use SendActivityAsync(IActivity, CancellationToken) instead.", false)]
        public IAsyncEnumerable<IActivity> AskQuestionAsync(IActivity activity, CancellationToken ct = default)
        {
            using (_logger.BeginScope("D2E:AskQuestionAsync"))
            {
                return SendActivityAsync(activity, ct);
            }
        }

        #endregion

        /// <inheritdoc/>
        [Obsolete("SubscribeAsync is Available to MSFT only at this time.", false)]
        public async IAsyncEnumerable<SubscribeEvent> SubscribeAsync(string conversationId, string? lastReceivedEventId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId), "A valid Conversation Id is required to use this method.");

            Uri uriExecute = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(Settings, conversationId, createSubscribeLink: true);

            var qbody = new SubscribeRequest();
            
            using HttpRequestMessage qreq = new()
            {
                Method = HttpMethod.Post,
                RequestUri = uriExecute,
                Headers =
                {
                    Accept = { s_EventStream }
                },
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(qbody)))
                {
                    Headers =
                    {
                        ContentType = s_ApplicationJson,
                    }
                }
            };

            // Setup headers. 
            qreq.Headers.Add("User-Agent", UserAgentHelper.UserAgentHeader);
            if (!string.IsNullOrEmpty(lastReceivedEventId))
            {
                // Add the last event Id header. This is used by the server to determine which events to send back in case of a disconnect.
                qreq.Headers.Add("Last-Event-Id", lastReceivedEventId);
            }

            await foreach (var activity in PostSubscribeRequestAsync(qreq, RequestTypes.ContinueSession, cancellationToken))
            {
                yield return activity;
            }

        }


        private async IAsyncEnumerable<IActivity> ExecuteRequestAction(IActivity request_activity, [EnumeratorCancellation] CancellationToken ct = default)
        {
            using (_logger.BeginScope("D2E:ExecuteRequestAction"))
            {
                AssertionHelpers.ThrowIfNull(request_activity, nameof(request_activity));
                string localConversationId = "";
                if (!string.IsNullOrEmpty(request_activity.Conversation?.Id))
                    localConversationId = request_activity.Conversation!.Id;
                else
                    localConversationId = _conversationId;

                Uri uriExecute = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(Settings, localConversationId);
                ExecuteTurnRequest qbody = new() { Activity = (Activity)request_activity };
                HttpRequestMessage qreq = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = uriExecute,
                    Headers =
                {
                    Accept = { s_EventStream }
                },
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(qbody)))
                    {
                        Headers =
                    {
                        ContentType = s_ApplicationJson,
                    }
                    }
                };
                qreq.Headers.Add("User-Agent", UserAgentHelper.UserAgentHeader);
                await foreach (var activity in PostActivityRequestAsync(qreq, RequestTypes.ExecuteAction, ct))
                {
                    yield return activity;
                }
            }
        }


        private async IAsyncEnumerable<SubscribeEvent> PostSubscribeRequestAsync(HttpRequestMessage req, RequestTypes requestType, [EnumeratorCancellation] CancellationToken ct = default)
        {
            AssertionHelpers.ThrowIfNull(req, nameof(req));

            using HttpResponseMessage resp = await SetupAndExecutePostRequest(req, ct).ConfigureAwait(false);

#if !NETSTANDARD
            using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
#else
            using Stream stream = await resp.Content.ReadAsStreamAsync();
#endif
            if (resp.Content?.Headers != null && resp.Content.Headers.ContentType!.Equals(s_EventStream))
            {
                // we requested a stream and got a stream, this is the expected path.
                var parser = SseParser.Create(stream);
                await foreach (var item in parser.EnumerateAsync(ct))
                {
                    if (item.EventType == "activity")
                    {
                        Activity activity = ProtocolJsonSerializer.ToObject<Activity>(item.Data);
                        yield return new SubscribeEvent(activity, item.EventId);
                    }
                }
            }
            else
            {
                // we requested a stream but did not get a stream, this is unexpected but we should try to parse any content we got back. 
                _logger.LogWarning("Expected an event stream but did not receive one. Attempting to parse response content as JSON.");
                if (requestType == RequestTypes.ContinueSession)
                {
                    var subscribeResponse = ProtocolJsonSerializer.ToObject<SubscribeResponse>(stream);
                    if (subscribeResponse is null)
                    {
                        throw new JsonException($"Response is empty. Expected {nameof(SubscribeResponse)} or `text/event-stream`");
                    }
                    foreach (var item in subscribeResponse.Activities)
                    {
                        yield return new SubscribeEvent(item, null);
                    }
                }
            }
        }


        /// <summary>
        /// Posts a request to Copilot Studio and returns the response as an async enumerable stream of activities.
        /// </summary>
        /// <param name="req">The request object to send to Copilot Studio.</param>
        /// <param name="ct">The cancellation token used to handle interruption requests.</param>
        /// <param name="requestType">The type of the request being sent, used for logging purposes.</param>
        /// <returns>An async enumerable stream of activities.</returns>
        /// <exception cref="System.ArgumentException">Thrown when required parameters are null.</exception>
        /// <exception cref="System.HttpRequestException">Thrown when the HTTP request fails.</exception>
        private async IAsyncEnumerable<IActivity> PostActivityRequestAsync(HttpRequestMessage req, RequestTypes requestType, [EnumeratorCancellation] CancellationToken ct = default)
        {
            AssertionHelpers.ThrowIfNull(req, nameof(req));

            using HttpResponseMessage resp = await SetupAndExecutePostRequest(req, ct).ConfigureAwait(false);

#if !NETSTANDARD
            using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
#else
            using Stream stream = await resp.Content.ReadAsStreamAsync();
#endif

            if (resp.Content?.Headers != null && resp.Content.Headers.ContentType!.Equals(s_EventStream))
            {
                // we requested a stream and got a stream, this is the expected path.
                var parser = SseParser.Create(stream);
                await foreach (var item in parser.EnumerateAsync(ct))
                {
                    if (item.EventType == "activity")
                    {
                        Activity activity = ProtocolJsonSerializer.ToObject<Activity>(item.Data);
                        switch (activity.Type)
                        {
                            case "message":
                                if (string.IsNullOrEmpty(_conversationId))
                                {
                                    // Only set the conversation Id locally the first time we see it, either from the header or the activity.
                                    // This is to handle the case where the conversation Id is not provided in the header but is provided in the activity.
                                    // We don't want to overwrite it if we already have it from the header.
                                    _conversationId = activity.Conversation.Id;
                                    _logger.LogInformation("Conversation ID: {ConversationId}", _conversationId);
                                }
                                yield return activity;
                                break;
                            default:
                                yield return activity;
                                break;
                        }
                    }
                }
            }
            else
            {
                // we requested a stream but did not get a stream, this is unexpected but we should try to parse any content we got back. 
                _logger.LogWarning("Expected an event stream but did not receive one. Attempting to parse response content as JSON.");
                if (requestType == RequestTypes.StartSession)
                {
                    var startResponse = ProtocolJsonSerializer.ToObject<StartResponse>(stream);
                    if (startResponse is null)
                    {
                        throw new JsonException($"Response is empty. Expected {nameof(StartResponse)} or `text/event-stream`");
                    }
                    foreach (var item in startResponse.Activities)
                    {
                        yield return item;
                    }
                }
                if (requestType == RequestTypes.ExecuteAction)
                {
                    var executeResponse = ProtocolJsonSerializer.ToObject<ExecuteTurnResponse>(stream);
                    if (executeResponse is null)
                    {
                        throw new JsonException($"Response is empty. Expected {nameof(ExecuteTurnResponse)} or `text/event-stream`");
                    }
                    foreach (var item in executeResponse.Activities)
                    {
                        yield return item;
                    }
                }
            }
        }

        private async Task<HttpResponseMessage> SetupAndExecutePostRequest(HttpRequestMessage req, CancellationToken ct)
        {
            HttpClient? httpClient;
            if (string.IsNullOrEmpty(_httpClientName))
            {
                httpClient = _httpClientFactory.CreateClient(); // Get the default client. 
            }
            else
            {
                httpClient = _httpClientFactory.CreateClient(_httpClientName);
            }

            if (httpClient is null)
            {
                throw new ArgumentException("Unable to create a connection to Copilot Studio Server");
            }

            if (_tokenProviderFunction != null)
            {
                // Set the access token header when its provided via an external token provider. 
                // If not done here the expectation is that the Token will be provided by an httpclient handler.
                string accessToken = string.Empty;
                if (req?.RequestUri != null)
                {
                    accessToken = await _tokenProviderFunction(req.RequestUri.ToString());
                }
                else
                {
                    accessToken = await _tokenProviderFunction(string.Empty);
                }

                AssertionHelpers.ThrowIfNull(req!, nameof(req));

                if (!string.IsNullOrEmpty(accessToken))
                {
                    req!.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
            }

            if (Settings.EnableDiagnostics)
            {
                _logger.LogDebug(">>> SEND TO {RequestUri}", req!.RequestUri);
            }
            HttpResponseMessage resp = await httpClient.SendAsync(req!, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Error sending request: {Status}", resp.StatusCode);
                if (resp.Content != null)
                {
#if !NETSTANDARD
                    string error = await resp.Content.ReadAsStringAsync(ct);
#else
                    string error = await resp.Content.ReadAsStringAsync();
#endif
                    _logger.LogError("Error: {Error}", error);
                    throw new HttpRequestException($"Error sending request: {resp.StatusCode}. {error}");
                }
                throw new HttpRequestException($"Error sending request: {resp.StatusCode}");
            }
            else
            {
                _logger.LogInformation("Request sent successfully");
            }

            // Check for the experimental URL header in the response headers
            if (resp.Headers.TryGetValues(CopilotStudioHeaderNames.D2EExperimentalUrl, out var values))
            {
                if (Settings.UseExperimentalEndpoint && string.IsNullOrEmpty(Settings.DirectConnectUrl))
                {
                    _IslandExperimentalUrl = values.FirstOrDefault() ?? string.Empty;
                    Settings.DirectConnectUrl = _IslandExperimentalUrl;
                    _logger.LogTrace("Island Experimental URL: {IslandExperimentalUrl}", _IslandExperimentalUrl);
                }
            }

            // Check for the conversation ID header in the response headers
            if (resp.Headers.TryGetValues(CopilotStudioHeaderNames.D2EConversationId, out var conversationIdValues))
            {
                _conversationId = conversationIdValues.FirstOrDefault() ?? string.Empty;
                _logger.LogTrace("Conversation ID: {ConversationId}", _conversationId);
            }

            if (Settings.EnableDiagnostics)
            {
                _logger.LogDebug("=====================================================");
                foreach (var item in resp.Headers)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item1 in item.Value.ToList())
                    {
                        sb.Append($"{item1} | ");
                    }
                    _logger.LogDebug("{HeaderKey} = {HeaderValues}", item.Key, sb.ToString());
                }
                _logger.LogDebug("=====================================================");
            }
            return resp;
        }
    }
}
