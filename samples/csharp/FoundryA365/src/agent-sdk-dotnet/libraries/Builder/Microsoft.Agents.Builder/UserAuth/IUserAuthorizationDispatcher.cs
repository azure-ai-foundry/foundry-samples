// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth
{
    internal interface IUserAuthorizationDispatcher
    {
        IUserAuthorization Default { get; }

        /// <summary>
        /// Get an authentication class via name
        /// </summary>
        /// <param name="handlerName">The name of the user authorization handler</param>
        /// <returns>The user authentication handler</returns>
        /// <exception cref="System.InvalidOperationException">When cannot find the class with given name</exception>
        IUserAuthorization Get(string handlerName);

        bool TryGet(string handlerName, out IUserAuthorization handler);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="handlerName">The name of the user authorization handler to use. If null, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        Task ResetStateAsync(ITurnContext turnContext, string handlerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a token for the user using the named handler.
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="handlerName">The name of the user authorization handler to use. If null, the default handler name is used.</param>
        /// <param name="forceSignIn"></param>
        /// <param name="exchangeConnection">Optional, passed to the named IUserAuthorization</param>
        /// <param name="exchangeScopes">Optional, passed to the named IUserAuthorization</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in status</returns>
        Task<SignInResponse> SignUserInAsync(ITurnContext turnContext, string handlerName, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out a user.
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="handlerName">Optional. The name of the user authorization handler to use. If not specified, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SignOutUserAsync(ITurnContext turnContext, string handlerName, CancellationToken cancellationToken = default);
    }
}