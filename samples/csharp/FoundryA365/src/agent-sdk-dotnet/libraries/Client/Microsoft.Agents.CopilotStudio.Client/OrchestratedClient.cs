// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.CopilotStudio.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.CopilotStudio.Client
{
    /// <summary>
    /// Client for externally orchestrated Copilot Studio conversations. This is intended for internal use only.
    /// Uses the ExternalOrchestration API to start conversations, invoke tools,
    /// handle user responses, and send conversation updates.
    /// </summary>
    public class OrchestratedClient : IOrchestratedClient
    {
        private static readonly MediaTypeWithQualityHeaderValue s_EventStream = new("text/event-stream");
        private static readonly MediaTypeWithQualityHeaderValue s_ApplicationJson = new("application/json");

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _httpClientName = string.Empty;
        private readonly ILogger _logger;
        private readonly Func<string, Task<string>>? _tokenProviderFunction = null;

        /// <summary>
        /// The connection settings for Copilot Studio.
        /// </summary>
        public ConnectionSettings Settings { get; }

        /// <summary>
        /// Returns the scope URL needed to connect to Copilot Studio from the connection settings.
        /// </summary>
        public static string ScopeFromSettings(ConnectionSettings settings) => PowerPlatformEnvironment.GetTokenAudience(settings);

        /// <summary>
        /// Creates an ExternalOrchestration client for Microsoft Copilot Studio hosted agents.
        /// </summary>
        /// <param name="settings">Configuration settings for connecting to Copilot Studio.</param>
        /// <param name="httpClientFactory">HTTP client factory to use when connecting.</param>
        /// <param name="logger">Logger for ExternalOrchestration operations.</param>
        /// <param name="httpClientName">Named HTTP client to use from the factory.</param>
        public OrchestratedClient(ConnectionSettings settings, IHttpClientFactory httpClientFactory, ILogger logger, string httpClientName = "orchestrated")
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _httpClientName = httpClientName;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates an ExternalOrchestration client with an external token provider. This is intended for internal use.
        /// </summary>
        /// <param name="settings">Configuration settings for connecting to Copilot Studio.</param>
        /// <param name="httpClientFactory">HTTP client factory to use when connecting.</param>
        /// <param name="tokenProviderFunction">Async function that accepts a scope URL and returns an access token.</param>
        /// <param name="logger">Logger for ExternalOrchestration operations.</param>
        /// <param name="httpClientName">Named HTTP client to use from the factory.</param>
        public OrchestratedClient(ConnectionSettings settings, IHttpClientFactory httpClientFactory, Func<string, Task<string>> tokenProviderFunction, ILogger logger, string httpClientName)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientName = httpClientName;
            _tokenProviderFunction = tokenProviderFunction ?? throw new ArgumentNullException(nameof(tokenProviderFunction));
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<OrchestratedResponse> StartConversationAsync(string conversationId, RequestProperties? requestProperties = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId), "A valid Conversation Id is required.");

            using (_logger.BeginScope("Orchestrated:StartConversation"))
            {
                _logger.LogTrace("Starting orchestrated conversation {ConversationId}", conversationId);

                var request = new OrchestratedTurnRequest
                {
                    Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.StartConversation }
                };

                await foreach (var result in ExecuteTurnAsync(conversationId, request, requestProperties, cancellationToken))
                {
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<OrchestratedResponse> InvokeToolAsync(string conversationId, ToolInvocationInput toolInputs, IActivity? activity = null, RequestProperties? requestProperties = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId), "A valid Conversation Id is required.");
            _ = toolInputs ?? throw new ArgumentNullException(nameof(toolInputs), "ToolInvocationInput is required.");

            using (_logger.BeginScope("Orchestrated:InvokeTool"))
            {
                _logger.LogTrace("Invoking tool {ToolSchemaName} in conversation {ConversationId}", toolInputs.ToolSchemaName, conversationId);

                var request = new OrchestratedTurnRequest
                {
                    Orchestration = new OrchestrationRequest
                    {
                        Operation = OrchestrationOperation.InvokeTool,
                        ToolInputs = toolInputs
                    },
                    Activity = (Activity?)activity
                };

                await foreach (var result in ExecuteTurnAsync(conversationId, request, requestProperties, cancellationToken))
                {
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<OrchestratedResponse> HandleUserResponseAsync(string conversationId, IActivity activity, RequestProperties? requestProperties = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId), "A valid Conversation Id is required.");
            _ = activity ?? throw new ArgumentNullException(nameof(activity), "An Activity is required.");

            using (_logger.BeginScope("Orchestrated:HandleUserResponse"))
            {
                _logger.LogTrace("Handling user response in conversation {ConversationId}", conversationId);

                var request = new OrchestratedTurnRequest
                {
                    Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.HandleUserResponse },
                    Activity = (Activity)activity
                };

                await foreach (var result in ExecuteTurnAsync(conversationId, request, requestProperties, cancellationToken))
                {
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<OrchestratedResponse> ConversationUpdateAsync(string conversationId, RequestProperties? requestProperties = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId), "A valid Conversation Id is required.");

            using (_logger.BeginScope("Orchestrated:ConversationUpdate"))
            {
                _logger.LogTrace("Sending conversation update for {ConversationId}", conversationId);

                var request = new OrchestratedTurnRequest
                {
                    Orchestration = new OrchestrationRequest { Operation = OrchestrationOperation.ConversationUpdate }
                };

                await foreach (var result in ExecuteTurnAsync(conversationId, request, requestProperties, cancellationToken))
                {
                    yield return result;
                }
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<OrchestratedResponse> ExecuteTurnAsync(string conversationId, OrchestratedTurnRequest request, RequestProperties? requestProperties = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
            _ = request ?? throw new ArgumentNullException(nameof(request));

            using (_logger.BeginScope("Orchestrated:ExecuteTurn"))
            {
                Uri uriExecute = PowerPlatformEnvironment.GetOrchestratedConnectionUrl(Settings, conversationId);

                HttpRequestMessage httpRequest = new()
                {
                    Method = HttpMethod.Post,
                    RequestUri = uriExecute,
                    Headers =
                    {
                        Accept = { s_EventStream }
                    },
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(request)))
                    {
                        Headers =
                        {
                            ContentType = s_ApplicationJson,
                        }
                    }
                };
                httpRequest.Headers.UserAgent.ParseAdd(UserAgentHelper.UserAgentHeader);
                httpRequest.Headers.Add(CopilotStudioHeaderNames.ConversationId, conversationId);
                httpRequest.Headers.Add(CopilotStudioHeaderNames.ClientRequestId, Guid.NewGuid().ToString());

                if (requestProperties != null)
                {
                    if (!string.IsNullOrEmpty(requestProperties.CorrelationId))
                    {
                        httpRequest.Headers.Add(CopilotStudioHeaderNames.CorrelationId, requestProperties.CorrelationId);
                    }
                    if (!string.IsNullOrEmpty(requestProperties.AgentVersion))
                    {
                        httpRequest.Headers.Add(CopilotStudioHeaderNames.AgentVersion, requestProperties.AgentVersion);
                    }
                    if (!string.IsNullOrEmpty(requestProperties.AcceptLanguage))
                    {
                        httpRequest.Headers.Add(CopilotStudioHeaderNames.AcceptLanguage, requestProperties.AcceptLanguage);
                    }
                }

                using HttpResponseMessage resp = await SetupAndExecutePostRequest(httpRequest, cancellationToken).ConfigureAwait(false);

#if !NETSTANDARD
                using Stream stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
#else
                using Stream stream = await resp.Content.ReadAsStreamAsync();
#endif

                if (resp.Content?.Headers != null && 
                    string.Equals(resp.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    // SSE stream response — the expected path
                    var parser = SseParser.Create(stream);
                    await foreach (var item in parser.EnumerateAsync(cancellationToken))
                    {
                        if (item.EventType == "activity")
                        {
                            Activity activity = ProtocolJsonSerializer.ToObject<Activity>(item.Data);
                            yield return new OrchestratedActivityResponse(activity);
                        }
                        else if (item.EventType == "state")
                        {
                            AgentStatePayload agentState = ProtocolJsonSerializer.ToObject<AgentStatePayload>(item.Data);
                            yield return new OrchestratedStateResponse(agentState);
                        }
                        else if (item.EventType == "error")
                        {
                            OrchestratedErrorEnvelope envelope = ProtocolJsonSerializer.ToObject<OrchestratedErrorEnvelope>(item.Data);
                            var errorPayload = envelope?.Error ?? new OrchestratedErrorPayload();
                            _logger.LogError(
                                "Orchestrated error received for ConversationId {ConversationId}, RequestUri {RequestUri}: {ErrorCode} ({Code}) - {Message}",
                                conversationId,
                                uriExecute,
                                errorPayload.ErrorCode,
                                errorPayload.Code,
                                errorPayload.Message);
                            yield return new OrchestratedErrorResponse(errorPayload);
                        }
                        else if (item.EventType == "end")
                        {
                            yield return new OrchestratedEndResponse(item.Data.ToString());
                            yield break; // End of stream reached, exit the loop
                        }
                    }
                }
                else
                {
                    // JSON response fallback
                    _logger.LogWarning("Expected an event stream but did not receive one. Attempting to parse response content as JSON.");

                    var turnResponse = ProtocolJsonSerializer.ToObject<OrchestratedTurnResponse>(stream);
                    if (turnResponse is null)
                    {
                        throw new JsonException($"Response is empty. Expected {nameof(OrchestratedTurnResponse)} or `text/event-stream`");
                    }
                    foreach (var item in turnResponse.Activities)
                    {
                        yield return new OrchestratedActivityResponse(item);
                    }
                    if (turnResponse.AgentState is not null)
                    {
                        yield return new OrchestratedStateResponse(turnResponse.AgentState);
                    }
                }
            }
        }

        /// <summary>
        /// Applies authentication (if configured) and sends the HTTP request, throwing on non-success status codes.
        /// </summary>
        private async Task<HttpResponseMessage> SetupAndExecutePostRequest(HttpRequestMessage req, CancellationToken ct)
        {
            HttpClient? httpClient;
            if (string.IsNullOrEmpty(_httpClientName))
            {
                httpClient = _httpClientFactory.CreateClient();
            }
            else
            {
                httpClient = _httpClientFactory.CreateClient(_httpClientName);
            }

            if (_tokenProviderFunction != null)
            {
                // Set the access token header when provided via an external token provider.
                // If not done here the expectation is that the Token will be provided by an HttpClient handler.
                string accessToken = string.Empty;
                if (req?.RequestUri != null)
                {
                    accessToken = await _tokenProviderFunction(req.RequestUri.ToString());
                }
                else
                {
                    accessToken = await _tokenProviderFunction(string.Empty);
                }
                if (!string.IsNullOrEmpty(accessToken))
                {
                    req!.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
                else
                {
                    _logger.LogWarning("Access token is empty. Request may fail if authentication is required.");
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(">>> SEND TO {RequestUri}", req!.RequestUri);
            }

            HttpResponseMessage resp = await httpClient.SendAsync(req!, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Error sending request: {Status}", resp.StatusCode);

                string errorContent = string.Empty;
#if !NETSTANDARD
                errorContent = await resp.Content.ReadAsStringAsync(ct);
#else
                errorContent = await resp.Content.ReadAsStringAsync();
#endif

                if (!string.IsNullOrEmpty(errorContent))
                {
                    _logger.LogError("Error content: {ErrorContent}", errorContent);
                }

                resp.EnsureSuccessStatusCode();
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(">>> RESPONSE STATUS: {StatusCode}", resp.StatusCode);
                if (resp.Headers != null)
                {
                    foreach (var header in resp.Headers)
                    {
                        _logger.LogDebug(">>> HEADER: {HeaderKey} = {HeaderValue}", header.Key, string.Join(", ", header.Value));
                    }
                }
                _logger.LogDebug("=====================================================");
            }

            return resp;
        }
    }
}
