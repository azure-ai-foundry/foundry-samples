// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Names for signin invoke operations in the token protocol.
    /// </summary>
    public static class SignInConstants
    {
        /// <summary>
        /// Name for the signin invoke to verify the 6-digit authentication code as part of sign-in.
        /// </summary>
        /// <remarks>
        /// This invoke operation includes a value containing a state property for the magic code or <see cref="Microsoft.Agents.Core.Models.SignInConstants.CancelledByUser"/>
        /// </remarks>
        public const string VerifyStateOperationName = "signin/verifyState";

        /// <summary>
        /// Name for signin invoke to perform a token exchange.
        /// </summary>
        /// <remarks>
        /// This invoke operation includes a value of the token exchange class.
        /// </remarks>
        public const string TokenExchangeOperationName = "signin/tokenExchange";

        /// <summary>
        /// Name for sign in failure during Teams SSO.
        /// </summary>
        public const string SignInFailure = "signin/failure";

        /// <summary>
        /// The EventActivity name when a token is sent to the Agent.
        /// </summary>
        public const string TokenResponseEventName = "tokens/response";

        /// <summary>
        /// The invoke operation used to exchange a SharePoint token for SSO.
        /// </summary>
        public const string SharePointTokenExchange = "cardExtension/token";

        /// <summary>
        /// Can be included in VerifyStateOperationName when user cancelled the signin.
        /// </summary>
        public const string CancelledByUser = "CancelledByUser";
    }
}
