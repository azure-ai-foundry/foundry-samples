// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.Telemetry.Adapter.Scopes;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// A factory to create REST clients to interact with a Channel Service.
    /// </summary>
    /// <remarks>
    /// Connector and UserToken client factory.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <exception cref="System.ArgumentException"></exception>
    /// <exception cref="System.InvalidOperationException">Thrown when an instance of <see cref="IAccessTokenProvider"/> is not found via <see cref="IConnections"/>.</exception>
    public class RestChannelServiceClientFactory : IChannelServiceClientFactory
    {
        private readonly string _tokenServiceEndpoint;
        private readonly string _tokenServiceAudience;
        private readonly string _botServiceAudience;
        private readonly int? _iMaxApxConversationIdLength;
        private readonly ILogger _logger;
        private readonly IConnections _connections;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <param name="configuration"></param>
        /// <param name="httpClientFactory">Used to create an HttpClient with the fullname of this class</param>
        /// <param name="connections"></param>
        /// <param name="tokenServiceEndpoint"></param>
        /// <param name="tokenServiceAudience"></param>
        /// <param name="botServiceAudience"></param>
        /// <param name="logger"></param>
        /// <param name="customClient">For testing purposes only.</param>
        public RestChannelServiceClientFactory(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IConnections connections,
            string tokenServiceEndpoint = AuthenticationConstants.BotFrameworkOAuthUrl,
            string tokenServiceAudience = AuthenticationConstants.BotFrameworkAudience,
            string botServiceAudience = AuthenticationConstants.BotFrameworkAudience,
            ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(configuration, nameof(configuration));

            _logger = logger ?? NullLogger.Instance;
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            var tokenEndpoint = configuration?.GetValue<string>($"{nameof(RestChannelServiceClientFactory)}:TokenServiceEndpoint");
            _tokenServiceEndpoint = string.IsNullOrWhiteSpace(tokenEndpoint)
                ? tokenServiceEndpoint ?? throw new ArgumentNullException(nameof(tokenServiceEndpoint))
                : tokenEndpoint;

            var tokenAudience = configuration?.GetValue<string>($"{nameof(RestChannelServiceClientFactory)}:TokenServiceAudience");
            _tokenServiceAudience = string.IsNullOrWhiteSpace(tokenAudience)
                ? tokenServiceAudience ?? throw new ArgumentNullException(nameof(tokenServiceAudience))
                : tokenAudience;

            var botAudience = configuration?.GetValue<string>($"{nameof(RestChannelServiceClientFactory)}:BotServiceAudience");
            _botServiceAudience = string.IsNullOrWhiteSpace(botAudience)
                ? botServiceAudience ?? throw new ArgumentNullException(nameof(botServiceAudience))
                : botAudience;

            _iMaxApxConversationIdLength = configuration?.GetValue<int?>($"{nameof(RestChannelServiceClientFactory)}:MaxApxConversationIdLength");
        }

        /// <inheritdoc />
        public Task<IConnectorClient> CreateConnectorClientAsync(ClaimsIdentity claimsIdentity, string serviceUrl, string audience, CancellationToken cancellationToken, IList<string> scopes = null, bool useAnonymous = false)
        {
            return CreateLegacyClientAsync(claimsIdentity, serviceUrl, audience, scopes, useAnonymous, cancellationToken);
        }

        public Task<IConnectorClient> CreateConnectorClientAsync(ITurnContext turnContext, string audience = null, IList<string> scopes = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeCreateConnectorClient(turnContext.Activity.ServiceUrl, scopes, turnContext.IsAgenticRequest());
            return telemetryScope.Wrap(() =>
            {
                if (turnContext.Activity.IsConnectorUser())
                {
                    // MCS Connector 
                    return CreateConnectorUserClientAsync(turnContext, cancellationToken);
                }

                if (!turnContext.Activity.IsAgenticRequest())
                {
                    // Non agentic, so use legacy tokens
                    return CreateLegacyClientAsync(turnContext.Identity, turnContext.Activity.ServiceUrl, audience, scopes, useAnonymous, cancellationToken);
                }

                // Use an Agentic token for the ConnectorClient
                return CreateAgenticClientAsync(turnContext, cancellationToken);
            });
        }

        /// <inheritdoc />
        public Task<IUserTokenClient> CreateUserTokenClientAsync(ClaimsIdentity claimsIdentity, bool? useAnonymous = false, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));

            using var telemetryScope = new ScopeCreateUserTokenClient(_tokenServiceEndpoint);

            var appId = claimsIdentity.GetIncomingAudience() ?? Guid.Empty.ToString();

            var anon = useAnonymous.HasValue ? (bool)useAnonymous : AgentClaims.AllowAnonymous(claimsIdentity);

            return Task.FromResult<IUserTokenClient>(new RestUserTokenClient(
                appId,
                new Uri(_tokenServiceEndpoint),
                _httpClientFactory,
                anon ? null : () =>
                {
                    try
                    {
                        var tokenAccess = _connections.GetTokenProvider(claimsIdentity, _tokenServiceEndpoint);
                        return tokenAccess.GetAccessTokenAsync(_tokenServiceAudience, [$"{_tokenServiceAudience}/.default"]);
                    }
                    catch (Exception ex)
                    {
                        // have to do it this way b/c of the lambda expression. 
                        throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                ErrorHelper.NullUserTokenProviderIAccessTokenProvider, ex, $"{claimsIdentity.GetIncomingAudience()}:{_tokenServiceEndpoint}");
                    }
                },
                typeof(RestChannelServiceClientFactory).FullName,
                _logger));
        }

        private Task<IConnectorClient> CreateLegacyClientAsync(ClaimsIdentity claimsIdentity, string serviceUrl, string audience = null, IList<string> scopes = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(serviceUrl, nameof(serviceUrl));

            return Task.FromResult<IConnectorClient>(new RestConnectorClient(
                new Uri(serviceUrl),
                _httpClientFactory,
                useAnonymous ? null : () =>
                {
                    try
                    {
                        audience ??= claimsIdentity.IsAgent() ? claimsIdentity.GetOutgoingAudience() : _botServiceAudience;
                        scopes ??= claimsIdentity.GetOutgoingScopes(defaultABSScopes: false); // Do not default ABS scopes because we want to use the value from config
                        var tokenAccess = _connections.GetTokenProvider(claimsIdentity, serviceUrl);
                        return tokenAccess.GetAccessTokenAsync(audience, scopes);
                    }
                    catch (Exception ex)
                    {
                        // have to do it this way b/c of the lambda expression. 
                        throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                ErrorHelper.NullIAccessTokenProvider, ex, $"{claimsIdentity.GetIncomingAudience()}:{serviceUrl}");
                    }
                },
                typeof(RestChannelServiceClientFactory).FullName,
                maxApxConversationIdLength: _iMaxApxConversationIdLength));
        }

        private Task<IConnectorClient> CreateConnectorUserClientAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult((IConnectorClient)new MCSConnectorClient(new Uri(turnContext.Activity.ServiceUrl), _httpClientFactory));
        }

        private Task<IConnectorClient> CreateAgenticClientAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult<IConnectorClient>(new RestConnectorClient(
                new Uri(turnContext.Activity.ServiceUrl),
                _httpClientFactory,
                () =>
                {
                    var connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
                    if (connection is IAgenticTokenProvider agenticTokenProvider)
                    {
                        try
                        {
                            if (turnContext.Activity.Recipient.Role.Equals(RoleTypes.AgenticIdentity))
                            {
                                return agenticTokenProvider.GetAgenticInstanceTokenAsync(
                                    turnContext.Activity.GetAgenticTenantId(),
                                    turnContext.Activity.GetAgenticInstanceId(),
                                    cancellationToken);
                            }

                            return agenticTokenProvider.GetAgenticUserTokenAsync(
                                turnContext.Activity.GetAgenticTenantId(),
                                turnContext.Activity.GetAgenticInstanceId(),
                                turnContext.Activity.GetAgenticUser(),
                                connection.ConnectionSettings.Scopes ?? [AuthenticationConstants.ApxProductionScope],
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            // have to do it this way b/c of the lambda expression. 
                            throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                    ErrorHelper.AgenticTokenProviderFailed, ex, turnContext.Activity.GetAgenticTenantId(), turnContext.Activity.GetAgenticInstanceId(), turnContext.Activity.GetAgenticUser(), turnContext.Activity.Recipient.Role);
                        }
                    }
                    else
                    {
                        // have to do it this way b/c of the lambda expression. 
                        throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                ErrorHelper.AgenticTokenProviderNotFound, null, $"{turnContext.Identity.GetIncomingAudience() ?? "<<NO_AUDIENCE>>"}:{turnContext.Activity.ServiceUrl ?? "<<NO_SERVICEURL>>"}");
                    }
                },
                typeof(RestChannelServiceClientFactory).FullName,
                maxApxConversationIdLength: _iMaxApxConversationIdLength));
        }
    }
}
