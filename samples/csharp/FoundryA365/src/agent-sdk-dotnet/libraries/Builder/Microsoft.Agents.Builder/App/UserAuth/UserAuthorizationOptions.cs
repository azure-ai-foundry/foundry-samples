// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.UserAuth
{
    /// <summary>
    /// Delegate for determining whether user authorization should be enabled for an incoming Activity.
    /// </summary>
    /// <remarks>
    /// AutoSignIn is determined by a call to a delegate that will return a bool.  This allow for control over which Activities
    /// will enable AutoSignIn.  <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions.AutoSignInOnForAny"/> and <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions.AutoSignInOff"/> can be used to provide a simple boolean result.
    /// </remarks>
    /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>True if authorization should be enabled. Otherwise, False.</returns>
    public delegate Task<bool> AutoSignInSelector(ITurnContext turnContext, CancellationToken cancellationToken);

    /// <summary>
    /// Options for user authorization.
    /// </summary>
    public class UserAuthorizationOptions
    {
        public readonly static AutoSignInSelector AutoSignInOnForAny = (context, cancellationToken) => Task.FromResult(true);
        public readonly static AutoSignInSelector AutoSignInOnForMessages = (context, cancellationToken) => Task.FromResult(context.Activity.IsType(ActivityTypes.Message));
        public readonly static AutoSignInSelector AutoSignInOff = (context, cancellationToken) => Task.FromResult(false);

        /// <summary>
        /// Creates UserAuthorizationOptions from IConfiguration and DI.
        /// </summary>
        /// <remarks>
        /// <code>
        /// "UserAuthorization": {
        ///   "DefaultHandlerName": "graph",
        ///   "AutoSignIn": true,
        ///   "Handlers": {
        ///     "graph": {
        ///     "Settings": {      // Settings are IUserAuthorization specific
        ///     }
        ///   }
        /// }
        /// </code>
        /// 
        /// <para>The "AutoSignIn" property will map to <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions.AutoSignInOnForAny"/> or <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions.AutoSignInOff"/>.  To provide a
        /// a custom selector, DI a <see cref="Microsoft.Agents.Builder.App.UserAuth.AutoSignInSelector"/>.</para>
        /// 
        /// The default Handler:Settings are mapped to <see cref="Microsoft.Agents.Builder.UserAuth.TokenService.OAuthSettings"/>.  These
        /// setting can be included in config:
        /// <code>
        /// "UserAuthorization": {
        ///   "Handlers": {
        ///     "Settings": {
        ///       "AzureBotOAuthConnectionName": "{{auzre-bot-connection-name}}",
        ///       "OBOConnectionName": "{{connections-name}}",
        ///       "OBOScopes": ["{{obo-scope}}"],
        ///       "Title": "{{signin-card-title}}",
        ///       "Text": "{{signin-card-button-text}}",
        ///       "InvalidSignInRetryMax": 2,
        ///       "InvalidSignInRetryMessage": "Please send code again",
        ///       "Timeout": {{timeout-ms}}
        ///     }
        ///   }
        /// </code>
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="configuration"></param>
        /// <param name="storage"></param>
        /// <param name="autoSignInSelector"></param>
        /// <param name="configKey"></param>
        public UserAuthorizationOptions(
            IServiceProvider sp, 
            ILoggerFactory loggerFactory,
            IConfiguration configuration, 
            IStorage storage = null,
            AutoSignInSelector autoSignInSelector = null, 
            string configKey = "UserAuthorization")
        {
            var section = configuration.GetSection(configKey);
            DefaultHandlerName = section.GetValue<string>(nameof(DefaultHandlerName));
            Storage = storage ?? sp.GetService<IStorage>() ?? throw new ArgumentNullException(nameof(storage));
            Dispatcher = new UserAuthorizationDispatcher(sp, loggerFactory, configuration, Storage, configKey: $"{configKey}:Handlers");

            var selectorInstance = autoSignInSelector ?? sp.GetService<AutoSignInSelector>();
            var autoSignIn = section.GetValue<bool>(nameof(AutoSignIn), true);
            AutoSignIn = selectorInstance ?? (autoSignIn ? AutoSignInOnForAny : AutoSignInOff);
        }

        /// <summary>
        /// Create UserAuthorizationOptions programmatically.
        /// </summary>
        /// <remarks>
        /// <code>
        ///   services.AddTransient&lt;IAgent&gt;(sp =>
        ///   {
        ///     var connections = sp.GetService&lt;IConnections&gt;();
        ///     var storage = sp.GetService&lt;IStorage&gt;();
        ///     
        ///     var options = new AgentApplicationOptions()
        ///     {
        ///       TurnStateFactory = () => new TurnState(storage),
        ///     
        ///       UserAuthorization = new UserAuthorizationOptions(connections, new AzureBotUserAuthorization("graph", storage, connections, new OAuthSettings())
        ///       {
        ///          DefaultHandlerName = "graph",
        ///          AutoSignin = AutoSignInOn
        ///       };
        ///     }
        ///     
        ///     var app = new AgentApplication(options);
        ///     
        ///     ...
        ///     
        ///     return app;
        ///   };
        /// </code>
        /// </remarks>
        /// <param name="loggerFactory">Typically from AgentApplicationOptions.LoggerFactory</param>
        /// <param name="storage">The IStorage to use for UserAuthorization flow state. This can be the same storage as elsewhere.</param>
        /// <param name="connections"></param>
        /// <param name="userAuthHandlers"></param>
        public UserAuthorizationOptions(ILoggerFactory loggerFactory, IStorage storage, IConnections connections, params IUserAuthorization[] userAuthHandlers)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Dispatcher = new UserAuthorizationDispatcher(loggerFactory, connections, userAuthHandlers);
            AutoSignIn = AutoSignInOnForAny;
        }

        [Obsolete("This constructor is deprecated. Use UserAuthorizationOptions(ILoggerFactory, IStorage, IConnections, params IUserAuthorization[])")]
        public UserAuthorizationOptions(IStorage storage, IConnections connections, params IUserAuthorization[] userAuthHandlers) : this(NullLoggerFactory.Instance, storage, connections, userAuthHandlers)
        {
        }

        internal IUserAuthorizationDispatcher Dispatcher { get; set; }

        internal IStorage Storage { get; set; }

        /// <summary>
        /// The default user authorization handler name to use for AutoSignIn.  If not specified, the first handler defined is
        /// used if Auto SignIn is enabled.
        /// </summary>
        public string DefaultHandlerName { get; set; }

        /// <summary>
        /// Indicates whether the Agent should start the sign in flow when the user sends a message to the Agent or triggers a message extension.
        /// If the selector returns false, the Agent will not start the sign in flow before routing the activity to the Agent logic.
        /// If the selector is not provided, the default selector returns true.
        /// </summary>
        /// <remarks>
        /// Auto SignIn will use the value of <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions.DefaultHandlerName"/> for the UserAuthorization handler to use.
        /// </remarks>
        public AutoSignInSelector? AutoSignIn { get; set; }

        /// <summary>
        /// Optional sign in failure message.  This is only used if the <see cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorization.OnUserSignInFailure"/> is not set.
        /// </summary>
        public Func<string, SignInResponse, IActivity[]> SignInFailedMessage { get; set; } = 
            (flowName, response) => [MessageFactory.Text(string.Format("Sign in for '{0}' completed without a token. Status={1}/{2}", flowName, response.Cause, response.Error?.Message))];
    }
}
