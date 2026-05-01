// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.Dialogs.Prompts
{
    /// <summary>
    /// Creates a new prompt that asks the user to sign in using the Single Sign On (SSO)
    /// service.
    /// </summary>
    /// <remarks>
    /// The prompt will attempt to retrieve the users current token and if the user isn't signed in, it
    /// will send them an `OAuthCard` containing a button they can press to sign in. Depending on the
    /// channel, the user will be sent through one of two possible sign in flows:
    ///
    /// - The automatic sign in flow where once the user signs in and the SSO service will forward the Agent
    /// the users access token using either an `event` or `invoke` activity.
    /// - The "magic code" flow where once the user signs in they will be prompted by the SSO
    /// service to send the Agent a six digit code confirming their identity. This code will be sent as a
    /// standard `message` activity.
    ///
    /// Both flows are automatically supported by the `OAuthPrompt` and the only thing you need to be
    /// careful of is that you don't block the `event` and `invoke` activities that the prompt might
    /// be waiting on.
    ///
    /// <blockquote>
    /// **Note**:
    /// You should avoid persisting the access token with your Agents other state. The 
    /// SSO service will securely store the token on your behalf. If you store it in your Agents state
    /// it could expire or be revoked in between turns.
    ///
    /// When calling the prompt from within a waterfall step you should use the token within the step
    /// following the prompt and then let the token go out of scope at the end of your function.
    /// </blockquote>
    ///
    /// ## Prompt Usage
    ///
    /// When used with your Agent's <see cref="DialogSet"/> you can simply add a new instance of the prompt as a named
    /// dialog using <see cref="DialogSet.Add(Dialog)"/>. You can then start the prompt from a waterfall step using either
    /// <see cref="DialogContext.BeginDialogAsync(string, object, CancellationToken)"/> or
    /// <see cref="DialogContext.PromptAsync(string, PromptOptions, CancellationToken)"/>. The user
    /// will be prompted to signin as needed and their access token will be passed as an argument to
    /// the callers next waterfall step.
    /// </remarks>
    public class OAuthPrompt : Dialog
    {
        private const string PersistedOptions = "options";
        private const string PersistedState = "state";
        private const string PersistedExpires = "expires";
        private const string PersistedCaller = "caller";

        private readonly OAuthPromptSettings _settings;
        private readonly PromptValidator<TokenResponse> _validator;
        private readonly OAuthFlow _dialogOAuthFlow;

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthPrompt"/> class.
        /// </summary>
        /// <param name="dialogId">The ID to assign to this prompt.</param>
        /// <param name="settings">Additional OAuth settings to use with this instance of the prompt.</param>
        /// <param name="validator">Optional, a <see cref="PromptValidator{FoundChoice}"/> that contains additional,
        /// custom validation for this prompt.</param>
        /// <remarks>The value of <paramref name="dialogId"/> must be unique within the
        /// <see cref="DialogSet"/> or <see cref="ComponentDialog"/> to which the prompt is added.</remarks>
        public OAuthPrompt(string dialogId, OAuthPromptSettings settings, PromptValidator<TokenResponse> validator = null)
            : base(dialogId)
        {
            if (string.IsNullOrWhiteSpace(dialogId))
            {
                throw new ArgumentNullException(nameof(dialogId));
            }

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _validator = validator;

            _dialogOAuthFlow = new OAuthFlow(_settings);
        }

        /// <summary>
        /// Shared implementation of the SetCallerInfoInDialogState function. This is intended for internal use, to
        /// consolidate the implementation of the OAuthPrompt and OAuthInput. Application logic should use
        /// those dialog classes.
        /// </summary>
        /// <param name="state">The dialog state.</param>
        /// <param name="context">ITurnContext.</param>
        public static void SetCallerInfoInDialogState(IDictionary<string, object> state, ITurnContext context)
        {
            state[PersistedCaller] = CreateCallerInfo(context);
        }

        /// <summary>
        /// Called when a prompt dialog is pushed onto the dialog stack and is being activated.
        /// </summary>
        /// <param name="dc">The dialog context for the current turn of the conversation.</param>
        /// <param name="options">Optional, additional information to pass to the prompt being started.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>If the task is successful, the result indicates whether the prompt is still
        /// active after the turn has been processed by the prompt.</remarks>
        public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(dc, nameof(dc));

            if (options is CancellationToken)
            {
                throw new ArgumentException($"{nameof(options)} cannot be a cancellation token", nameof(options));
            }

            if (options != null && options is not PromptOptions)
            {
                throw new ArgumentException($"Parameter {nameof(options)} should be an instance of to {nameof(PromptOptions)} if provided", nameof(options));
            }

            var opt = (PromptOptions)options;
            if (opt != null)
            {
                // Ensure prompts have input hint set
                if (opt.Prompt != null && string.IsNullOrEmpty(opt.Prompt.InputHint))
                {
                    opt.Prompt.InputHint = InputHints.AcceptingInput;
                }

                if (opt.RetryPrompt != null && string.IsNullOrEmpty(opt.RetryPrompt.InputHint))
                {
                    opt.RetryPrompt.InputHint = InputHints.AcceptingInput;
                }
            }

            // Initialize state
            var timeout = _settings.Timeout ?? (int)OAuthPromptSettings.DefaultTimeoutValue.TotalMilliseconds;
            var state = dc.ActiveDialog.State;
            state[PersistedOptions] = opt;
            state[PersistedState] = new Dictionary<string, object>
            {
                { Prompt<int>.AttemptCountKey, 0 },
            };

            state[PersistedExpires] = DateTime.UtcNow.AddMilliseconds(timeout);
            SetCallerInfoInDialogState(state, dc.Context);

            var token = await _dialogOAuthFlow.BeginFlowAsync(dc.Context, opt?.Prompt == null ? null : () => Task.FromResult(opt?.Prompt), cancellationToken).ConfigureAwait(false);
            if (token != null)
            {
                // Return token
                return await dc.EndDialogAsync(token, cancellationToken).ConfigureAwait(false);
            }

            return EndOfTurn;
        }

        /// <summary>
        /// Called when a prompt dialog is the active dialog and the user replied with a new activity.
        /// </summary>
        /// <param name="dc">The dialog context for the current turn of conversation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>If the task is successful, the result indicates whether the dialog is still
        /// active after the turn has been processed by the dialog.
        /// <para>The prompt generally continues to receive the user's replies until it accepts the
        /// user's reply as valid input for the prompt.</para></remarks>
        public override async Task<DialogTurnResult> ContinueDialogAsync(DialogContext dc, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(dc, nameof(dc));

            // Check for timeout
            var state = dc.ActiveDialog.State;
            var expires = (DateTime) state[PersistedExpires];
            var isMessage = dc.Context.Activity.Type == ActivityTypes.Message;

            try
            {
                var tokenResponse = await _dialogOAuthFlow.ContinueFlowAsync(dc.Context, expires, cancellationToken).ConfigureAwait(false);

                if (IsTokenResponseEvent(dc.Context))
                {
                    // fixup the turnContext's state context if this was received from a skill host caller
                    var callerInfo = (CallerInfo)dc.ActiveDialog.State[PersistedCaller];
                    if (callerInfo != null)
                    {
                        // set the ServiceUrl to the caller host's Url
                        dc.Context.Activity.ServiceUrl = callerInfo.CallerServiceUrl;

                        // recreate a ConnectorClient and set it in TurnState so replies use the correct one
                        var serviceUrl = dc.Context.Activity.ServiceUrl;
                        var audience = callerInfo.Scope;
                        var connectorClient = await CreateConnectorClientAsync(dc.Context, audience, cancellationToken).ConfigureAwait(false);
                        dc.Context.Services.Set<IConnectorClient>(connectorClient);
                    }
                }

                var recognized = new PromptRecognizerResult<TokenResponse>()
                {
                    Succeeded = tokenResponse != null,
                    Value = tokenResponse,
                };

                var promptState = (IDictionary<string, object>)state[PersistedState];
                var promptOptions = (PromptOptions)state[PersistedOptions];

                // Increment attempt count
                promptState[Prompt<int>.AttemptCountKey] = (int) promptState[Prompt<int>.AttemptCountKey] + 1;

                // Validate the return value
                var isValid = recognized.Succeeded;
                if (_validator != null && recognized.Succeeded)
                {
                    var promptContext = new PromptValidatorContext<TokenResponse>(dc.Context, recognized, promptState, promptOptions);
                    isValid = await _validator(promptContext, cancellationToken).ConfigureAwait(false);
                }

                // Return recognized value or re-prompt
                if (isValid)
                {
                    return await dc.EndDialogAsync(recognized.Value, cancellationToken).ConfigureAwait(false);
                }
                else if (isMessage && _settings.EndOnInvalidMessage)
                {
                    // If EndOnInvalidMessage is set, complete the prompt with no result.
                    return await dc.EndDialogAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                if (!dc.Context.Responded && isMessage && promptOptions?.RetryPrompt != null)
                {
                    await dc.Context.SendActivityAsync(promptOptions.RetryPrompt, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                // if the token fetch request times out, complete the prompt with no result.
                return await dc.EndDialogAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return EndOfTurn;
        }

        /// <summary>
        /// Attempts to get the user's token.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful and user already has a token or the user successfully signs in,
        /// the result contains the user's token.</remarks>
        public async Task<TokenResponse> GetUserTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            return await UserTokenClientWrapper.GetUserTokenAsync(turnContext, _settings.AzureBotOAuthConnectionName, magicCode: null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Signs out the user.
        /// </summary>
        /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await UserTokenClientWrapper.SignOutUserAsync(turnContext, _settings.AzureBotOAuthConnectionName, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<IConnectorClient> CreateConnectorClientAsync(ITurnContext turnContext, string audience, CancellationToken cancellationToken)
        {
            var clientFactory = turnContext.Services.Get<IChannelServiceClientFactory>();
            if (clientFactory != null)
            {
                return await clientFactory.CreateConnectorClientAsync(turnContext, audience, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            throw new NotSupportedException("OAuthFlow: IChannelServiceClientFactory is not supported. Was an IChannelServiceClientFactory registered?");
        }

        private static bool IsTokenResponseEvent(ITurnContext turnContext)
        {
            var activity = turnContext.Activity;
            return activity.Type == ActivityTypes.Event && activity.Name == SignInConstants.TokenResponseEventName;
        }

        private static CallerInfo CreateCallerInfo(ITurnContext turnContext)
        {
            if (turnContext.Identity as ClaimsIdentity != null && AgentClaims.IsAgentClaim(turnContext.Identity))
            {
                return new CallerInfo
                {
                    CallerServiceUrl = turnContext.Activity.ServiceUrl,
                    Scope = AgentClaims.GetOutgoingAppIdClaim(turnContext.Identity),
                };
            }

            return null;
        }

        private class CallerInfo
        {
            public string CallerServiceUrl { get; set; }

            public string Scope { get; set; }
        }
    }
}
