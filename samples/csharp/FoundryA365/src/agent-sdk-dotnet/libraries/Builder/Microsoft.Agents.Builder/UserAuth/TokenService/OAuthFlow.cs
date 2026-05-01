// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Creates a new prompt that asks the user to sign in using the Token Service.
    /// service.
    /// </summary>
    /// <remarks>
    /// <para>The prompt will attempt to retrieve the users current token and if the user isn't signed in, it
    /// will send them an `OAuthCard|SigninCard` containing a button they can press to signin. Depending on the
    /// channel, the user will be sent through one of two possible signin flows:</para>
    ///
    /// <para>- The automatic signin flow where once the user signs in and the SSO service will forward the Agent
    /// the users access token using either an `event` or `invoke` activity.</para>
    /// <para>- The "magic code" flow where once the user signs in they will be prompted by the SSO
    /// service to send the Agent a six digit code confirming their identity. This code will be sent as a
    /// standard `message` activity.</para>
    ///
    /// <para>Both flows are automatically supported by the `OAuthFlow` and the only thing you need to be
    /// careful of is that you don't block the `event` and `invoke` activities that the prompt might
    /// be waiting on.</para>
    /// </remarks>
    /// <param name="settings"></param>
    /// <param name="storage"></param>
    public class OAuthFlow(OAuthSettings settings)
    {
        private readonly OAuthSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public OAuthSettings Settings => _settings;

        public virtual async Task<TokenResponse> BeginFlowAsync(ITurnContext turnContext, Func<Task<IActivity>>? promptFactory, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            // Attempt to get the users token
            var output = await UserTokenClientWrapper.GetTokenOrSignInResourceAsync(turnContext, _settings.AzureBotOAuthConnectionName, magicCode: null, cancellationToken).ConfigureAwait(false);
            if (output != null && output?.TokenResponse != null)
            {
                // Return token
                return output.TokenResponse;
            }

            // Prompt user to login
            await SendOAuthCardAsync(
                turnContext,
                output?.SignInResource,
                promptFactory, cancellationToken).ConfigureAwait(false);

            return null;
        }

        /// <summary>
        /// Called when a prompt dialog is the active dialog and the user replied with a new activity.
        /// </summary>
        /// <param name="turnContext">The ITurnContext for the current turn of conversation.</param>
        /// <param name="expires">The DateTime the exchange expires.  Typically this would be stored after the BeginFlowAsync and used here.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="TokenResponse"/>The token response.</returns>
        /// <remarks>If successful, the result indicates whether the exchange is still
        /// active after the turn has been processed.
        /// <para>The prompt generally continues to receive the user's replies until it accepts the
        /// user's reply as valid input for the prompt.</para></remarks>
        /// <exception cref="System.TimeoutException"/>
        public virtual async Task<TokenResponse> ContinueFlowAsync(ITurnContext turnContext, DateTime expires, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            // Check for timeout
            var hasTimedOut = HasTimedOut(turnContext, expires);
            if (hasTimedOut)
            {
                if (IsTokenExchangeRequestInvoke(turnContext))
                {
                    // We must respond to the Invoke.
                    var tokenExchangeRequest = turnContext.Activity.Value != null ? ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>(turnContext.Activity.Value) : null;
                    await SendInvokeResponseAsync(
                        turnContext,
                        HttpStatusCode.BadRequest,
                        new TokenExchangeInvokeResponse
                        {
                            Id = tokenExchangeRequest?.Id,
                            ConnectionName = _settings.AzureBotOAuthConnectionName,
                            FailureDetail = "The Agent received a 'signin/tokenExchange' but had timed out.",
                        }, cancellationToken).ConfigureAwait(false);
                }

                throw new TimeoutException("OAuthFlow timeout");
            }

            // Recognize token
            return await RecognizeTokenAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Signs out the user.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Shared implementation of the SendOAuthCardAsync function. This is intended for internal use, to
        /// consolidate the implementation of the OAuthPrompt and OAuthInput. Application logic should use
        /// those dialog classes.
        /// </summary>
        /// <param name="settings">OAuthSettings.</param>
        /// <param name="turnContext">ITurnContext.</param>
        /// <param name="signInResource"></param>
        /// <param name="promptFactory">Creates signin prompt</param>
        /// <param name="cancellationToken">CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        private async Task SendOAuthCardAsync(ITurnContext turnContext, SignInResource signInResource, Func<Task<IActivity>>? promptFactory, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            IActivity prompt = null;
            if (promptFactory != null)
            {
                prompt = await promptFactory().ConfigureAwait(false);
                if (prompt != null && prompt.Attachments == null)
                {
                    prompt.Attachments = [];
                }
            }

            if (prompt == null)
            {
                prompt = Activity.CreateMessageActivity();
                prompt.Attachments = [];
            }

            // Ensure prompt initialized

            // Append appropriate card if missing
            if (!ChannelSupportsOAuthCard(turnContext.Activity.ChannelId))
            {
                if (!prompt.Attachments.Any(a => a.Content is SigninCard))
                {
                    signInResource ??= await UserTokenClientWrapper.GetSignInResourceAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false);
                    prompt.Attachments.Add(new Attachment
                    {
                        ContentType = SigninCard.ContentType,
                        Content = new SigninCard
                        {
                            Text = _settings.Text,
                            Buttons =
                            [
                                new CardAction
                                {
                                    Title = _settings.Title,
                                    Value = signInResource.SignInLink,
                                    Type = ActionTypes.Signin,
                                },
                            ],
                        },
                    });
                }
            }
            else if (!prompt.Attachments.Any(a => a.Content is OAuthCard))
            {
                var cardActionType = ActionTypes.Signin;
                signInResource ??= await UserTokenClientWrapper.GetSignInResourceAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false);

                string value;
                if (_settings.ShowSignInLink != null && _settings.ShowSignInLink == false ||
                    _settings.ShowSignInLink == null && !ChannelRequiresSignInLink(turnContext.Activity.ChannelId))
                {
                    value = null;
                }
                else
                {
                    value = signInResource.SignInLink;
                }

                TokenExchangeResource? tokenExchangeResource = null;
                if (_settings.EnableSso == true)
                {
                    tokenExchangeResource = signInResource.TokenExchangeResource;
                }

                prompt.Attachments.Add(new Attachment
                {
                    ContentType = OAuthCard.ContentType,
                    Content = new OAuthCard
                    {
                        Text = _settings.Text,
                        ConnectionName = _settings.AzureBotOAuthConnectionName,
                        Buttons =
                        [
                            new CardAction
                            {
                                Title = _settings.Title,
                                Text = _settings.Text,
                                Type = cardActionType,
                                Value = value
                            },
                        ],
                        TokenExchangeResource = tokenExchangeResource,
                        TokenPostResource = signInResource.TokenPostResource
                    },
                });
            }

            // Set input hint
            if (string.IsNullOrEmpty(prompt.InputHint))
            {
                prompt.InputHint = InputHints.AcceptingInput;
            }

            prompt.ChannelData = turnContext.Activity.ChannelData;
            await turnContext.SendActivityAsync(prompt, cancellationToken).ConfigureAwait(false);
        }

        // Handles exchanging the code/token for a TokenResponse.
        //
        // Teams SSO notes:
        //    Teams will send an "signin/tokenExchange" Invoke.  If the Token Service Exchange result in a "ConsentRequired" (HTTP 400), Teams
        //    expects the InvokeResponse.Status to be 412.  This will cause Teams to get Consent and send another "signin/tokenExchange" with
        //    the exchangeable token.
        //
        //    In the event of a critical exception (500 or unknown from Token Service), an InvokeResponse.Status 400 should be returned which
        //    will cause Teams to stop.
        //
        // Return null if the flow is still pending.
        // Throws:
        //    ErrorResponseException
        //    UserCancelledException
        //    ConsentRequiredException - caller should noop if no specific action required.
        private async Task<TokenResponse> RecognizeTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            TokenResponse result = null;
            if (IsTokenResponseEvent(turnContext))
            {
                result = ProtocolJsonSerializer.ToObject<TokenResponse>(turnContext.Activity.Value);
            }
            else if (IsSignInFailureInvoke(turnContext))
            {
                await SendInvokeResponseAsync(turnContext, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);
                var errorResponse = new ErrorResponse() { Error = ProtocolJsonSerializer.ToObject<Error>(turnContext.Activity.Value) };
                throw new ErrorResponseException($"SignInFailure: ({errorResponse.Error.Code}) {errorResponse.Error.Message}") { Body = errorResponse };
            }
            else if (IsVerificationInvoke(turnContext))
            {
                var value = turnContext.Activity.Value.ToJsonElements();
                var magicCode = value.ContainsKey("state") ? turnContext.Activity.Value.ToJsonElements()["state"].ToString() : null;

                if (!string.IsNullOrEmpty(magicCode) && magicCode.Equals(SignInConstants.CancelledByUser, StringComparison.OrdinalIgnoreCase))
                {
                    // This happens either by the user closing the SignIn window, including if there was a error on the sign in process (say
                    // misconfigured OAuthConnection.  We don't get enough information to know the difference.
                    await SendInvokeResponseAsync(turnContext, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);
                    throw new UserCancelledException();
                }

                // Getting the token follows a different flow in Teams. At the signin completion, Teams
                // will send the bot an "invoke" activity that contains a "magic" code. This code MUST
                // then be used to try fetching the token from UserToken service within some time
                // period. We try here. If it succeeds, we return 200 with an empty body. If it fails
                // with a retriable error, we return 500. Teams will re-send another invoke in this case.
                // If it fails with a non-retriable error, we return 404. Teams will not retry in that case.
                try
                {
                    result = await UserTokenClientWrapper.GetUserTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, magicCode, cancellationToken).ConfigureAwait(false);

                    if (result != null)
                    {
                        await SendInvokeResponseAsync(turnContext, HttpStatusCode.OK, null, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendInvokeResponseAsync(turnContext, HttpStatusCode.NotFound, null, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    await SendInvokeResponseAsync(turnContext, HttpStatusCode.InternalServerError, null, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (IsTokenExchangeRequestInvoke(turnContext))
            {
                var tokenExchangeRequest = turnContext.Activity.Value != null ? ProtocolJsonSerializer.ToObject<TokenExchangeInvokeRequest>(turnContext.Activity.Value) : null;

                if (tokenExchangeRequest == null)
                {
                    await SendInvokeResponseAsync(
                        turnContext,
                        HttpStatusCode.BadRequest,
                        new TokenExchangeInvokeResponse
                        {
                            Id = null,
                            ConnectionName = _settings.AzureBotOAuthConnectionName,
                            FailureDetail = "The Agent received an InvokeActivity that is missing a TokenExchangeInvokeRequest value. This is required to be sent with the InvokeActivity.",
                        }, cancellationToken).ConfigureAwait(false);
                }
                else if (tokenExchangeRequest.ConnectionName != _settings.AzureBotOAuthConnectionName)
                {
                    await SendInvokeResponseAsync(
                        turnContext,
                        HttpStatusCode.BadRequest,
                        new TokenExchangeInvokeResponse
                        {
                            Id = tokenExchangeRequest.Id,
                            ConnectionName = _settings.AzureBotOAuthConnectionName,
                            FailureDetail = "The Agent received an InvokeActivity with a TokenExchangeInvokeRequest containing a ConnectionName that does not match the ConnectionName expected by the Agents active OAuthFlow. Ensure these names match when sending the InvokeActivityInvalid ConnectionName in the TokenExchangeInvokeRequest",
                        }, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    TokenResponse tokenExchangeResponse = null;

                    try
                    {
                        var exchangeRequest = new TokenExchangeRequest { Token = tokenExchangeRequest.Token };
                        tokenExchangeResponse = await UserTokenClientWrapper.ExchangeTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, exchangeRequest, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        bool isConsentRequired = ex as ErrorResponseException != null && ((ErrorResponseException)ex).Body.Error.Code.Equals(Error.ConsentRequiredCode);
                        if (!isConsentRequired)
                        {
                            // This is a critical error.  Either request failure, or OAuth Connection misconfiguration.
                            // A 400 seems to cause Teams to not retry.  412 or 500 does not.
                            // Callers should catch and clean up state because all bets are off.
                            await SendInvokeResponseAsync(
                                turnContext,
                                HttpStatusCode.BadRequest,
                                new TokenExchangeInvokeResponse
                                {
                                    Id = tokenExchangeRequest.Id,
                                    ConnectionName = _settings.AzureBotOAuthConnectionName,
                                    FailureDetail = ex.Message,
                                }, cancellationToken).ConfigureAwait(false);
                            throw;
                        }
                    }

                    if (tokenExchangeResponse == null || string.IsNullOrEmpty(tokenExchangeResponse.Token))
                    {
                        // This triggers the "consent required" flow in Teams.  Teams will prompt the user to consent to SSO and
                        // then resend the InvokeActivity with the token for exchange.  If we return a 400, Teams will not retry
                        // and the user is not given the chance to consent.
                        await SendInvokeResponseAsync(
                            turnContext,
                            HttpStatusCode.PreconditionFailed,
                            new TokenExchangeInvokeResponse
                            {
                                Id = tokenExchangeRequest.Id,
                                ConnectionName = _settings.AzureBotOAuthConnectionName,
                                FailureDetail = "The Agent is unable to exchange token. Proceed with regular login.",
                            }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await SendInvokeResponseAsync(
                            turnContext,
                            HttpStatusCode.OK,
                            new TokenExchangeInvokeResponse
                            {
                                Id = tokenExchangeRequest.Id,
                                ConnectionName = _settings.AzureBotOAuthConnectionName,
                            }, cancellationToken).ConfigureAwait(false);

                        result = tokenExchangeResponse;
                    }
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                if (!string.IsNullOrEmpty(turnContext.Activity.Text))
                {
                    // regex to check if code supplied is a 6 digit numerical code (hence, a magic code).
                    var magicCodeRegex = new Regex(@"(\d{6})");
                    var matched = magicCodeRegex.Match(turnContext.Activity.Text);
                    if (matched.Success)
                    {
                        // Note that if result is null, it is likely because the magicCode was invalid.
                        // The Token Service doesn't provide any way to determine this though.
                        result = await UserTokenClientWrapper.GetUserTokenAsync(
                            turnContext,
                            _settings.AzureBotOAuthConnectionName,
                            magicCode: matched.Value,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return result;
        }

        public static bool IsTokenResponseEvent(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Event && activity.Name == SignInConstants.TokenResponseEventName;
        }

        public static bool IsVerificationInvoke(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Invoke && activity.Name == SignInConstants.VerifyStateOperationName;
        }

        public static bool IsTokenExchangeRequestInvoke(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Invoke && activity.Name == SignInConstants.TokenExchangeOperationName;
        }

        public static bool IsSignInFailureInvoke(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Invoke && activity.Name == SignInConstants.SignInFailure;
        }

        private static bool ChannelSupportsOAuthCard(ChannelId channelId)
        {
            return channelId.ToString() switch
            {
                Channels.Cortana or Channels.Skype or Channels.Skypeforbusiness => false,
                _ => true,
            };
        }

        public static bool ChannelRequiresSignInLink(ChannelId channelId)
        {
            return channelId.Channel switch
            {
                Channels.Msteams => true,
                _ => false,
            };
        }

        internal static bool HasTimedOut(ITurnContext context, DateTime expires)
        {
            var isMessage = context.Activity.Type == ActivityTypes.Message;

            // If the incoming Activity is a message, or an Activity Type normally handled by OAuthPrompt,
            // check to see if this OAuthPrompt Expiration has elapsed, and end the dialog if so.
            var isTimeoutActivityType = isMessage
                            || IsTokenResponseEvent(context)
                            || IsVerificationInvoke(context)
                            || IsTokenExchangeRequestInvoke(context);
            return isTimeoutActivityType && DateTime.Compare(DateTime.UtcNow, expires) > 0;
        }

        private static async Task SendInvokeResponseAsync(ITurnContext turnContext, HttpStatusCode statusCode, object body, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(
                new Activity
                {
                    Type = ActivityTypes.InvokeResponse,
                    Value = new InvokeResponse
                    {
                        Status = (int)statusCode,
                        Body = body,
                    },
                }, cancellationToken).ConfigureAwait(false);
        }
    }
}
