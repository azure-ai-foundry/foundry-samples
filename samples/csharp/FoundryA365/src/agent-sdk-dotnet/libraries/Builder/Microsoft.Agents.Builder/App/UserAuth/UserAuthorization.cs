// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.UserAuth
{
    public delegate Task AuthorizationFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken);

    /// <summary>
    /// UserAuthorization supports and extensible number of OAuth flows.
    /// 
    /// Auto Sign In:
    /// If enabled in <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions"/>, sign in starts automatically after the first Message the user sends.  When
    /// the sign in is complete, the turn continues with the original message. On failure, <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorization.OnUserSignInFailure(System.Func{Microsoft.Agents.Builder.ITurnContext, Microsoft.Agents.Builder.State.ITurnState, string, Microsoft.Agents.Builder.UserAuth.SignInResponse, System.Threading.CancellationToken, System.Threading.Tasks.Task})"/>
    /// is called.
    /// 
    /// </summary>
    /// <remarks>
    /// This is always executed in the context of a turn for the user in <see cref="Microsoft.Agents.Builder.ITurnContext.Activity"/>.
    /// </remarks>
    public class UserAuthorization
    {
        private readonly AutoSignInSelector? _startSignIn;
        private readonly IUserAuthorizationDispatcher _dispatcher;
        private readonly UserAuthorizationOptions _options;
        private readonly AgentApplication _app;
        private readonly List<HandlerToken> _authTokens = [];

        /// <summary>
        /// Callback when user sign in fail
        /// </summary>
        private AuthorizationFailure _userSignInFailureHandler;

        public string DefaultHandlerName { get; private set; }

        public UserAuthorization(AgentApplication app, UserAuthorizationOptions options)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _dispatcher = options.Dispatcher;

            if (_options.AutoSignIn != null)
            {
                _startSignIn = _options.AutoSignIn;
            }
            else
            {
                // If AutoSignIn wasn't specified, default to true. 
                _startSignIn = (context, cancellationToken) => Task.FromResult(true);
            }

            DefaultHandlerName = _options.DefaultHandlerName ?? _dispatcher.Default.Name;

            if (!_dispatcher.TryGet(DefaultHandlerName, out _))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.UserAuthorizationDefaultHandlerNotFound, null, DefaultHandlerName);
            }
        }

        [Obsolete("Use Task<string> GetTurnTokenAsync(ITurnContext, string) instead")]
        public string GetTurnToken(string handlerName)
        {
           return _authTokens.Find(ht => ht.Handler.Equals(handlerName))?.TokenResponse.Token;
        }

        /// <summary>
        /// Return a previously acquired token.
        /// </summary>
        /// <remarks>
        /// This is a mechanism to access a previously acquired user token during the turn. It should be
        /// considered a method to get a token for each handler, not as a way to initiate the signin process.
        /// This will also handle refreshing the token if expired between initial acquisition and use.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="handlerName"></param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetTurnTokenAsync(ITurnContext turnContext, string handlerName = default, CancellationToken cancellationToken = default)
        {
            return await ExchangeTurnTokenAsync(turnContext, handlerName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> ExchangeTurnTokenAsync(ITurnContext turnContext, string handlerName = default, string exchangeConnection = default, IList<string> exchangeScopes = default, CancellationToken cancellationToken = default)
        {
            if (_authTokens == null || _authTokens.Count == 0)
            {
                return null;
            }

            TokenResponse token;
            if (string.IsNullOrEmpty(handlerName))
            {
                // Cached turn tokens are stored in the order of addition (the order on the route).
                // If no handler name is provided, return the first.
                token = _authTokens[0].TokenResponse;
                handlerName = _authTokens[0].Handler;
            }
            else
            {
                token = _authTokens.Find(ht => ht.Handler.Equals(handlerName))?.TokenResponse;
            }

            if (token != null)
            {
                // Return a non-expired non-exchangeable token.
                if (!turnContext.IsAgenticRequest() && !token.IsExchangeable)
                {
                    var diff = token.Expiration - DateTimeOffset.UtcNow;
                    if (diff.HasValue && diff?.TotalMinutes >= 5)
                    {
                        return token.Token;
                    }
                }

                // Refresh an exchangeable or expired token
                var handler = _dispatcher.Get(handlerName);
                var response = await handler.GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
                if (response?.Token != null)
                {
                    if (!token.IsExchangeable)
                    {
                        // Refresh cahce with the latest non-exchangeable token.
                        CacheToken(handlerName, response);
                    }
                    return response.Token;
                }

                // This is a critical error since the only way we are here is we had a token (user signed in) yet
                // didn't get a token back.  We are not it a place to handle a multi-turn sign in.
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UnexpectedAuthorizationState, null, handlerName);
            }

            return null;
        }

        public IList<TurnToken> GetTurnTokens()
        {
            return [.. _authTokens.Select(ht => new TurnToken(ht.Handler, ht.TokenResponse.Token))];
        }

        public async Task SignOutUserAsync(ITurnContext turnContext, ITurnState turnState, string? flowName = null, CancellationToken cancellationToken = default)
        {
            var flow = flowName ?? DefaultHandlerName;
            DeleteCachedToken(flow);
            await DeleteSignInStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            await _dispatcher.SignOutUserAsync(turnContext, flow, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The handler function is called when the user sign in flow fails
        /// </summary>
        /// <remarks>
        /// This is called for either Manual or Auto SignIn flows.  However, normally expected AgentApplication
        /// Turn process has not been performed during an Auto Sign In.  This handler should be used to send failure message to the user
        /// and the turn ended.
        /// </remarks>
        /// <param name="handler">The handler function to call when the user failed to signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public void OnUserSignInFailure(AuthorizationFailure handler)
        {
            _userSignInFailureHandler = handler;
        }

        /// <summary>
        /// This starts/continues the sign in flow.
        /// </summary>
        /// <remarks>
        /// This should be called to start or continue the user auth until true is returned, which indicates sign in is complete.
        /// When complete, the token is cached and can be access via <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorization.GetTurnTokenAsync"/>.
        /// <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorization.OnUserSignInFailure(System.Func{Microsoft.Agents.Builder.ITurnContext, Microsoft.Agents.Builder.State.ITurnState, string, Microsoft.Agents.Builder.UserAuth.SignInResponse, System.Threading.CancellationToken, System.Threading.Tasks.Task})"/> is called on an error completion.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="handlerName">The name of the handler defined in <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions"/></param>
        /// <param name="forceAuto"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>false indicates the sign in is not complete, or that further processing of the Activity should stop.</returns>
        internal async Task<bool> StartOrContinueSignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName = null, bool forceAuto = false, CancellationToken cancellationToken = default)
        {
            // If a flow is active, continue that.
            var signInState = await GetSignInStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
            string? activeFlowName = signInState.ActiveHandler;
            bool flowContinuation = activeFlowName != null;
            bool autoSignIn = forceAuto || (_startSignIn != null && await _startSignIn(turnContext, cancellationToken));

            if (autoSignIn || flowContinuation)
            {
                // Auth flow hasn't start yet.
                activeFlowName ??= handlerName ?? DefaultHandlerName;

                // Get token or start flow for specified flow.
                SignInResponse response = await _dispatcher.SignUserInAsync(
                    turnContext,
                    activeFlowName,
                    forceSignIn: !flowContinuation,
                    exchangeConnection: signInState.RuntimeOBOConnectionName,
                    exchangeScopes: signInState.RuntimeOBOScopes,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (response.Status == SignInStatus.Pending)
                {
                    if (!flowContinuation)
                    {
                        // Bank the incoming Activity so it can be executed after sign in is complete.
                        signInState.ContinuationActivity = turnContext.Activity;
                        signInState.ActiveHandler = activeFlowName;

                        await SetSignInStateAsync(turnContext, signInState, cancellationToken).ConfigureAwait(false);
                    }

                    // Flow started, pending user input
                    return false;
                }

                // Hard error and the flow is cancelled.  It is possible there is a scenario where a retry is desired, but
                // unhandled at the moment.
                if (response.Status == SignInStatus.Error)
                {
                    // Clear user auth state
                    await _dispatcher.ResetStateAsync(turnContext, activeFlowName, cancellationToken).ConfigureAwait(false);
                    await DeleteSignInStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (turnContext.Activity.IsType(ActivityTypes.Invoke))
                    {
                        if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                        {
                            _app.Logger.LogWarning("UserAuthorization: InvokeResponse not set for '{Invoke.Name}'", turnContext.Activity.Name);

                            // For Invoke activities, set the InvokeResponse since the user won't seen any sent activities.
                            await turnContext.SendActivityAsync(new Activity
                            {
                                Type = ActivityTypes.InvokeResponse,
                                Value = new InvokeResponse
                                {
                                    Status = 500,
                                    Body = new
                                    {
                                        activity = new
                                        {
                                            id = turnContext.Activity.Id,
                                            channelId = turnContext.Activity.ChannelId.Channel,
                                            type = turnContext.Activity.Type,
                                            name = turnContext.Activity.Name,
                                            conversation = turnContext.Activity.Conversation,
                                            from = turnContext.Activity.From,
                                            recipient = turnContext.Activity.Recipient,
                                        },
                                        cause = response.Cause.ToString(),
                                        failureDetail = response.Error?.Message ?? string.Empty
                                    }
                                }
                            }, cancellationToken).ConfigureAwait(false);
                        }
                        return false;
                    }

                    if (_userSignInFailureHandler != null)
                    {
                        await _userSignInFailureHandler(turnContext, turnState, activeFlowName, response, signInState.ContinuationActivity, cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    await turnContext.SendActivitiesAsync(
                        _options.SignInFailedMessage == null ? [MessageFactory.Text("SignIn Failed")] : _options.SignInFailedMessage(activeFlowName, response),
                        cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (response.Status == SignInStatus.Complete)
                {
                    await DeleteSignInStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    CacheToken(activeFlowName, response.TokenResponse);

                    if (signInState.ContinuationActivity != null)
                    {
                        // If the current activity matches the one used to trigger sign in, then
                        // this is because the user received a token that didn't involve a multi-turn
                        // flow.  No further action needed.
                        if (!ProtocolJsonSerializer.Equals(signInState.ContinuationActivity, turnContext.Activity))
                        {
                            // Since we could be handling an Invoke in this turn, and Teams has expectation for Invoke response times,
                            // we need to continue the conversation in a different turn with the original Activity that triggered sign in.
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await turnContext.Adapter.ProcessProactiveAsync(
                                turnContext.Identity,
                                signInState.ContinuationActivity.ApplyConversationReference(turnContext.Activity.GetConversationReference(), isIncoming: true),
                                _app,
                                cancellationToken).ConfigureAwait(false);
                            return false;
                        }
                    }
                }
            }
            else if (!flowContinuation)
            {
                if (turnContext.Activity.IsType(ActivityTypes.Invoke)
                    && (turnContext.Activity.Name == SignInConstants.TokenExchangeOperationName || turnContext.Activity.Name == SignInConstants.VerifyStateOperationName))
                {
                    _app.Logger.LogWarning("UserAuthorization: Received Invoke:{Invoke.Name} but an OAuthFlow is not active for user '{User.Id}' using handler '{Handler.Name}'", 
                        turnContext.Activity.Name, turnContext.Activity.From.Id, handlerName ?? DefaultHandlerName);

                    // This would mean we've received an OAuth related request, but we aren't in an active flow.
                    // For Invoke activities, set the InvokeResponse since the user won't seen any sent activities.
                    await turnContext.SendActivityAsync(new Activity
                    {
                        Type = ActivityTypes.InvokeResponse,
                        Value = new InvokeResponse
                        {
                            Status = (int)HttpStatusCode.BadRequest,
                            Body = new
                            {
                                activity = new
                                {
                                    id = turnContext.Activity.Id,
                                    channelId = turnContext.Activity.ChannelId.Channel,
                                    type = turnContext.Activity.Type,
                                    name = turnContext.Activity.Name,
                                    conversation = turnContext.Activity.Conversation,
                                    from = turnContext.Activity.From,
                                    recipient = turnContext.Activity.Recipient,
                                },
                                cause = AuthExceptionReason.Other.ToString(),
                                failureDetail = $"The user is not in an active OAuthFlow for handler:{handlerName ?? DefaultHandlerName}"
                            }
                        }
                    }, cancellationToken).ConfigureAwait(false);
                }
            }

            // Sign in is complete (or never started if Auto Sign in is false)
            // AgentApplication will perform normal ITurnContext.Activity routing to Agent.
            return true;
        }

        internal async Task<bool> GetSignedInTokensAsync(ITurnContext turnContext, string[] handlerList, CancellationToken cancellationToken)
        {
            int fetched = 0;
            foreach (var handlerName in handlerList)
            {
                var handler = _dispatcher.Get(handlerName);
                if (handler != null)
                {
                    var token = await handler.GetRefreshedUserTokenAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (token != null)
                    {
                        fetched++;
                        CacheToken(handlerName, token);
                    }
                }
            }

            return fetched == handlerList.Length;
        }

        private void CacheToken(string name, TokenResponse tokenResponse)
        {
            var existing = _authTokens.Find(ht => ht.Handler.Equals(name));
            if (existing != null)
            {
                existing.TokenResponse = tokenResponse;
                return;
            }
            _authTokens.Add(new HandlerToken() { Handler = name, TokenResponse = tokenResponse });
        }

        private void DeleteCachedToken(string name)
        {
            _authTokens.RemoveAll(ht => ht.Handler.Equals(name));
        }

        private async Task<SignInState> GetSignInStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            var items = await _options.Storage.ReadAsync([GetStorageKey(turnContext)], cancellationToken).ConfigureAwait(false);
            if (items.TryGetValue(GetStorageKey(turnContext), out var state) && state is SignInState signInState)
            {
                 return signInState;
            }
            return new();
        }

        private Task SetSignInStateAsync(ITurnContext turnContext, SignInState state, CancellationToken cancellationToken)
        {
            return _options.Storage.WriteAsync(new Dictionary<string, object> { { GetStorageKey(turnContext), state } }, cancellationToken);
        }

        private Task DeleteSignInStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return _options.Storage.DeleteAsync(new[] { GetStorageKey(turnContext) }, cancellationToken);
        }

        private static string GetStorageKey(ITurnContext turnContext)
        {
            // This key is used since per conversation, a user can only have one active flow at a time.
            var channelId = turnContext.Activity.ChannelId?.Channel ?? throw new InvalidOperationException("invalid activity-missing ChannelId");
            var userId = turnContext.Activity.From?.Id ?? throw new InvalidOperationException("invalid activity-missing From.Id");
            return $"oauth/{channelId}/{userId}/userAuthorizationState";
        }
    }

    class SignInState
    {
        public bool IsActive() => !string.IsNullOrEmpty(ActiveHandler);
        public string ActiveHandler { get; set; }
        public IActivity ContinuationActivity { get; set; }
        public string RuntimeOBOConnectionName { get; set; }
        public IList<string> RuntimeOBOScopes { get; set; }
    }

    class HandlerToken
    {
        public string Handler { get; set; }
        public TokenResponse TokenResponse { get; set; }
    }

    public class TurnToken(string handler, string token)
    {
        public string Handler { get; private set; } = handler;
        public string Token { get; private set; } = token;
    }
}
