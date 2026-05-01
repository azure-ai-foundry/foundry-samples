// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Record for a token exchange request that is sent as part of Authentication. </summary>
    public class TokenExchangeResource
    {
        public TokenExchangeResource() { }

        /// <summary> Initializes a new instance of TokenExchangeResource. </summary>
        /// <param name="id"> A unique identifier for this token exchange instance. </param>
        /// <param name="uri"> The application ID / resource identifier with which to exchange a token on behalf of. </param>
        /// <param name="providerId">
        /// The identifier of the provider with which to attempt a token exchange
        /// A value of null or empty will default to Azure Active Directory
        /// </param>
        public TokenExchangeResource(string id = default, string uri = default, string providerId = default)
        {
            Id = id;
            Uri = uri;
            ProviderId = providerId;
        }

        /// <summary> A unique identifier for this token exchange instance. </summary>
        public string Id { get; set; }
        /// <summary> The application ID / resource identifier with which to exchange a token on behalf of. </summary>
        public string Uri { get; set; }
        /// <summary>
        /// The identifier of the provider with which to attempt a token exchange
        /// A value of null or empty will default to Azure Active Directory
        /// </summary>
        public string ProviderId { get; set; }
    }
}
