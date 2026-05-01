// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Telemetry.Authorization.Scopes;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Handles OAuth using the Azure Bot Token Service.
    /// </summary>
    public class AzureBotUserAuthorization : OBOExchange, IUserAuthorization
    {
        // OAuthFlow is used here as a path for back-compat since Dialog.OAuthPrompt uses
        // it.  Long term, OAuthPrompt should probably migrate to the higher level classes
        // and this impl refactored.
        private readonly OAuthFlow _flow;
        private readonly ILogger _logger;
        private readonly OAuthSettings _settings;
        private readonly IStorage _storage;

        /// <summary>
        /// Required constructor for UserAuthorizationDispatcher.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="storage"></param>
        /// <param name="connections"></param>
        /// <param name="configurationSection"></param>
        /// <param name="logger"></param>
        public AzureBotUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
            : this(name, storage, connections, GetOAuthSettings(configurationSection), logger)
        {

        }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        /// <param name="connections">The connections to use.</param>
        /// <param name="logger">Optional logger.</param>
        public AzureBotUserAuthorization(string name, IStorage storage, IConnections connections, OAuthSettings settings, ILogger logger = null)
            : base(connections)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _flow = new OAuthFlow(settings);
            _logger = logger ?? NullLogger<ILogger>.Instance;
        }

        public string Name { get; private set; }

        protected override OBOSettings GetOBOSettings()
        {
            return _settings;
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeAzureBotSignIn(Name, exchangeConnection, exchangeScopes, turnContext);

            if (forceSignIn || await IsValidActivity(turnContext, cancellationToken).ConfigureAwait(false))
            {
                var token = await OnFlowTurn(turnContext, cancellationToken).ConfigureAwait(false);

                try
                {
                    return await HandleOBO(turnContext, token, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _storage.DeleteAsync([GetStorageKey(turnContext)], cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeAzureBotSignOut(Name, _settings.AzureBotOAuthConnectionName, turnContext);
            await ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            using var telemetryScope = new ScopeAzureBotToken(Name, exchangeConnection, exchangeScopes);
            var response = await UserTokenClientWrapper.GetUserTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, null, cancellationToken).ConfigureAwait(false);
            return await HandleOBO(turnContext, response, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
        }

        private static OAuthSettings GetOAuthSettings(IConfigurationSection config)
        {
            var settings = config.Get<OAuthSettings>();

            if (settings.OBOScopes == null)
            {
                // try reading as a string to compensate for users just setting a non-array string
                var configScope = config.GetSection(nameof(OAuthSettings.OBOScopes)).Get<string>();
                if (!string.IsNullOrEmpty(configScope))
                {
                    settings.OBOScopes = [configScope];
                }
            }

            return settings;
        }

        private async Task<bool> IsValidActivity(ITurnContext context, CancellationToken cancellationToken = default)
        {
            // Catch user input in Teams where the flow has timed out.  Otherwise we get stuck in "flow active" forever.
            if (context.Activity.ChannelId.IsParentChannel(Channels.Msteams) && context.Activity.IsType(ActivityTypes.Message))
            {
                var state = await GetFlowStateAsync(context, cancellationToken).ConfigureAwait(false);
                if (state.FlowStarted && OAuthFlow.HasTimedOut(context, state.FlowExpires))
                {
                    throw new AuthException("Authorization flow timed out.", AuthExceptionReason.Timeout);
                }
            }

            var isMatch = context.Activity.IsType(ActivityTypes.Message);

            isMatch |= context.Activity.IsType(ActivityTypes.Invoke) &&
                context.Activity.Name == SignInConstants.VerifyStateOperationName;

            isMatch |= context.Activity.IsType(ActivityTypes.Invoke) &&
                context.Activity.Name == SignInConstants.TokenExchangeOperationName;

            isMatch |= context.Activity.IsType(ActivityTypes.Invoke) &&
                context.Activity.Name == SignInConstants.SignInFailure;

            isMatch |= context.Activity.IsType(ActivityTypes.Event) &&
                context.Activity.Name == SignInConstants.TokenResponseEventName;

            return isMatch;
        }

        private async Task<TokenResponse> OnFlowTurn(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var state = await GetFlowStateAsync(turnContext, cancellationToken).ConfigureAwait(false);

            // Handle start or continue of the flow.
            // Either path can throw.  This is intentionally not trapping the exception to give the caller the chance
            // to determine if a retry is applicable.  Otherwise, caller should call ResetState.
            var flowTask = !state.FlowStarted
                            ? OnGetOrStartFlowAsync(turnContext, state, cancellationToken)
                            : OnContinueFlow(turnContext, state, cancellationToken);
            var tokenResponse = await flowTask.ConfigureAwait(false);

            await SaveFlowStateAsync(turnContext, state, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(tokenResponse?.Token))
            {
                return null;
            }

            return tokenResponse;
        }

        private async Task<TokenResponse> OnGetOrStartFlowAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            // If the user is already signed in, tokenResponse will be non-null
            var tokenResponse = await _flow.BeginFlowAsync(
                turnContext,
                null,
                cancellationToken).ConfigureAwait(false);

            // If a TokenResponse is returned, there was a cached token already.  Otherwise, start the process of getting a new token.
            if (tokenResponse == null)
            {
                var expires = DateTime.UtcNow.AddMilliseconds(_settings.Timeout ?? OAuthSettings.DefaultTimeoutValue.TotalMilliseconds);

                state.FlowStarted = true;
                state.FlowExpires = expires;
            }

            return tokenResponse;
        }

        private async Task<TokenResponse> OnContinueFlow(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            TokenResponse tokenResponse;

            try
            {
                tokenResponse = await _flow.ContinueFlowAsync(turnContext, state.FlowExpires, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new AuthException("Authorization flow timed out.", AuthExceptionReason.Timeout);
            }
            catch (UserCancelledException)
            {
                throw new AuthException("User cancelled authorization", AuthExceptionReason.UserCancelled);
            }

            if (tokenResponse == null)
            {
                if (!OAuthFlow.IsTokenExchangeRequestInvoke(turnContext))
                {
                    state.ContinueCount++;
                    if (state.ContinueCount >= _settings.InvalidSignInRetryMax)
                    {
                        // The only way this happens is if C2 sent a bogus code
                        throw new AuthException("Retry max", AuthExceptionReason.InvalidSignIn);
                    }

                    await turnContext.SendActivityAsync(_settings.InvalidSignInRetryMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                // token still pending.
                return null;
            }

            state.FlowStarted = false;
            return tokenResponse;
        }

        private async Task<FlowState> GetFlowStateAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var key = GetStorageKey(turnContext);
            var items = await _storage.ReadAsync([key], cancellationToken).ConfigureAwait(false);
            return items.TryGetValue(key, out object value) ? (FlowState)value : new FlowState();
        }

        private async Task SaveFlowStateAsync(ITurnContext turnContext, FlowState state, CancellationToken cancellationToken)
        {
            var key = GetStorageKey(turnContext);
            var items = new Dictionary<string, object>()
                {
                    { key, state }
                };
            await _storage.WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }

        private string GetStorageKey(ITurnContext turnContext)
        {
            var channelId = turnContext.Activity.ChannelId ?? throw new InvalidOperationException("invalid activity-missing channelId");
            var conversationId = turnContext.Activity.Conversation?.Id ?? throw new InvalidOperationException("invalid activity-missing Conversation.Id");
            return $"oauth/{Name}/{channelId}/{conversationId}/flowState";
        }
    }

    class FlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
        public int ContinueCount = 0;
    }
}
