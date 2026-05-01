// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// A type containing information for single sign-on.
    /// </summary>
    public class SignInResource
    {
        public SignInResource() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.SignInResource"/> class.
        /// </summary>
        /// <param name="signInLink">Sign-in link.</param>
        /// <param name="tokenExchangeResource">Token exchange resource.</param>
        public SignInResource(string signInLink = default, TokenExchangeResource tokenExchangeResource = default)
        {
            SignInLink = signInLink;
            TokenExchangeResource = tokenExchangeResource;
        }

        /// <summary>
        /// Gets or sets the sign-in link.
        /// </summary>
        /// <value>The sign-in link.</value>
        public string SignInLink { get; set; }

        /// <summary>
        /// Gets or sets additional properties that can be used for token exchange operations.
        /// </summary>
        /// <value>The additional properties can be used for token exchange operations.</value>
        public TokenExchangeResource TokenExchangeResource { get; set; }

        /// <summary>
        /// Gets or sets additional properties that can be used for direct token posting operations.
        /// </summary>
        /// <value>The additional properties can be used for token posting operations.</value>
        public TokenPostResource TokenPostResource { get; set; }
    }
}
