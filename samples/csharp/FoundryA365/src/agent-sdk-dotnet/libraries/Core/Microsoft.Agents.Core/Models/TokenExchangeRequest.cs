// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> The TokenExchangeRequest. </summary>
    public class TokenExchangeRequest
    {
        public TokenExchangeRequest()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenExchangeRequest"/> class.
        /// </summary>
        /// <param name="uri">URI.</param>
        /// <param name="token">Token.</param>
        public TokenExchangeRequest(string uri = default, string token = default)
        {
            Uri = uri;
            Token = token;
        }

        /// <summary> Gets or sets the uri. </summary>
        public string Uri { get; set; }
        /// <summary> Gets or sets the token. </summary>
        public string Token { get; set; }
    }
}
