// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.RestClients
{
    /// <summary>
    /// AgentSignIn operations.
    /// </summary>
    internal interface IAgentSignIn
    {
        /// <summary>Get sign-in URL with HTTP Message.</summary>
        /// <param name='state'>State.</param>
        /// <param name='codeChallenge'>Code challenge.</param>
        /// <param name='emulatorUrl'>Emulator URL.</param>
        /// <param name='finalRedirect'>Final redirect.</param>
        /// <param name='customHeaders'>The headers that will be added to request.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        Task<string> GetSignInUrlAsync(string state, string codeChallenge = default, string emulatorUrl = default, string finalRedirect = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the raw signin link to be sent to the user for signin for a connection name.
        /// </summary>
        /// <param name="connectionName">Name of the auth connection to use.</param>
        /// <param name="activity">The <see cref="Microsoft.Agents.Core.Models.Activity"/> from which to derive the token exchange state.</param>
        /// <param name="finalRedirect">The final URL that the OAuth flow will redirect to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="codeChallenge"></param>
        /// <param name="emulatorUrl"></param>
        /// <param name="state"></param>
        /// <returns>A <see cref="System.Threading.Tasks.Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<SignInResource> GetSignInResourceAsync(string state, string codeChallenge = null, string emulatorUrl = null, string finalRedirect = null, CancellationToken cancellationToken = default);
    }
}
