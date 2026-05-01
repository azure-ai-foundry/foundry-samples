// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Builder.App;



#if !NETSTANDARD
using System.Runtime.Loader;
#endif

namespace Microsoft.Agents.Builder.UserAuth
{
    /// <summary>
    /// Loads and dispatches request to a named instance of IUserAuthorization.
    /// </summary>
    /// <remarks>
    /// This utilizes type loading to support extensibility.
    /// </remarks>
    internal class UserAuthorizationDispatcher : IUserAuthorizationDispatcher
    {
        private readonly Dictionary<string, UserAuthorizationDefinition> _userAuthHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<UserAuthorizationDispatcher> _logger;
        private readonly IStorage _storage;
        private readonly IConnections _connections;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="connections"></param>
        /// <param name="userAuthHandlers"></param>
        public UserAuthorizationDispatcher(ILoggerFactory loggerFactory, IConnections connections, params IUserAuthorization[] userAuthHandlers)
        {
            _loggerFactory = loggerFactory ?? AgentApplicationOptions.DefaultLoggerFactory;
            _logger = loggerFactory.CreateLogger<UserAuthorizationDispatcher>();
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));

            if (userAuthHandlers == null || userAuthHandlers.Length == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthorizationHandlers, null);
            }

            foreach(var authenticator in userAuthHandlers)
            {
                _userAuthHandlers.Add(authenticator.Name, new UserAuthorizationDefinition() {  Instance = authenticator });
            }
        }

        /// <summary>
        /// Create dispatcher from config.
        /// </summary>
        /// <code>
        /// "UserAuthorization": {
        ///   "graph": {
        ///     "Assembly": null,  // Optional, defaults to OAuthAuthentication Assembly
        ///     "Type": null,      // Optional, defaults to OAuthAuthentication Type
        ///     "Settings": {      // Settings are Type specific, for OAuthAuthentication, any OAuthSettings property.
        ///     }
        ///   }
        /// }
        /// </code>
        /// <remarks>
        /// <see cref="OAuthAuthentication"/> is the default <see cref="IUserAuthorization"/> unless type loading is specified.  The `Settings`
        /// node for the defaults is properties in <see cref="OAuthSettings"/>.  This User Authentication is performed with
        /// the Azure Bot Token Service using `OAuth Connections` defined on the Azure Bot.
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="configuration"></param>
        /// <param name="storage"></param>
        /// <param name="configKey"></param>
        public UserAuthorizationDispatcher(IServiceProvider sp, ILoggerFactory loggerFactory, IConfiguration configuration, IStorage storage, string configKey = "UserAuthentication")
        {
            _loggerFactory = loggerFactory ?? AgentApplicationOptions.DefaultLoggerFactory;
            _logger = loggerFactory.CreateLogger<UserAuthorizationDispatcher>();
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _connections = sp.GetService<IConnections>();

            var configDict = configuration.GetSection(configKey).Get<Dictionary<string, UserAuthorizationDefinition>>();
            _userAuthHandlers = new(configDict, StringComparer.OrdinalIgnoreCase);

            if (_userAuthHandlers.Count == 0)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.NoUserAuthorizationHandlers, null);
            }

#if !NETSTANDARD
            var assemblyLoader = new UserAuthorizationModuleLoader(AssemblyLoadContext.Default, _logger);
#else
            var assemblyLoader = new UserAuthorizationModuleLoader(AppDomain.CurrentDomain, _logger);
#endif

            foreach (var definition in _userAuthHandlers)
            {
                definition.Value.Constructor = assemblyLoader.GetProviderConstructor(definition.Key, definition.Value.Assembly, definition.Value.Type);
            }
        }

        /// <inheritdoc/>
        public IUserAuthorization Default => _userAuthHandlers.Count > 0 
            ? GetHandlerInstance(_userAuthHandlers.First().Key) 
            : throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationHandlerNotFound, null);

        /// <inheritdoc/>
        public async Task<SignInResponse> SignUserInAsync(ITurnContext turnContext, string handlerName, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            IUserAuthorization auth = Get(handlerName);
            TokenResponse token;
            try
            {
                token = await auth.SignInUserAsync(turnContext, forceSignIn, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"SignUserInAsync exception: {ex}");

                SignInResponse newResponse = new(SignInStatus.Error)
                {
                    Error = ex,
                    Cause = AuthExceptionReason.Exception
                };
                if (ex is AuthException authEx)
                {
                    newResponse.Cause = authEx.Cause;
                }

                return newResponse;
            }

            if (!string.IsNullOrEmpty(token?.Token))
            {
                return new SignInResponse(SignInStatus.Complete)
                {
                    TokenResponse = token,
                };
            }

            return new SignInResponse(SignInStatus.Pending);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, string handlerName, CancellationToken cancellationToken = default)
        {
            IUserAuthorization auth = Get(handlerName);
            await auth.SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, string handlerName, CancellationToken cancellationToken = default)
        {
            await Get(handlerName).ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        public IUserAuthorization Get(string handleName)
        {
            if (string.IsNullOrEmpty(handleName))
            {
                return Default;
            }

            return GetHandlerInstance(handleName.Trim());
        }

        public bool TryGet(string handlerName, out IUserAuthorization handler)
        {
            if (string.IsNullOrEmpty(handlerName))
            {
                handler = Default;
                return true;
            }

            if (_userAuthHandlers.TryGetValue(handlerName.Trim(), out UserAuthorizationDefinition handlerDefinition))
            {
                handler = GetHandlerInstance(handlerName, handlerDefinition);
                return true;
            }

            handler = default;
            return false;
        }

        private IUserAuthorization GetHandlerInstance(string handlerName)
        {
            if (!_userAuthHandlers.TryGetValue(handlerName, out UserAuthorizationDefinition handlerDefinition))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.UserAuthorizationHandlerNotFound, null, handlerName);
            }
            return GetHandlerInstance(handlerName, handlerDefinition);
        }

        private IUserAuthorization GetHandlerInstance(string handlerName, UserAuthorizationDefinition handlerDefinition)
        {
            if (handlerDefinition.Instance != null)
            {
                // Return existing instance.
                return handlerDefinition.Instance;
            }

            try
            {
                // Construct the provider
                handlerDefinition.Instance = handlerDefinition.Constructor.Invoke([handlerName, _storage, _connections, handlerDefinition.Settings, _loggerFactory.CreateLogger(handlerDefinition.Constructor.DeclaringType)]) as IUserAuthorization;
                return handlerDefinition.Instance;
            }
            catch (Exception ex)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateUserAuthorizationHandler, ex, handlerDefinition.Type);
            }
        }
    }
}
