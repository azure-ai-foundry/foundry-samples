// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    public class TokenOrSignInResourceResponse
    {
        public TokenOrSignInResourceResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenOrSignInResourceResponse"/> class.
        /// </summary>
        /// <param name="tokenResponse">Token response.</param>
        /// <param name="signInResource">SignIn resource.</param>
        public TokenOrSignInResourceResponse(TokenResponse tokenResponse = default, SignInResource signInResource = default)
        {
            TokenResponse = tokenResponse;
            SignInResource = signInResource;
        }

        /// <summary>
        /// Gets or sets the TokenResponse.
        /// </summary>
        public TokenResponse TokenResponse { get; set; }

        /// <summary>
        /// Gets or sets the SignInResource.
        /// </summary>
        public SignInResource SignInResource { get; set; }
    }
}
