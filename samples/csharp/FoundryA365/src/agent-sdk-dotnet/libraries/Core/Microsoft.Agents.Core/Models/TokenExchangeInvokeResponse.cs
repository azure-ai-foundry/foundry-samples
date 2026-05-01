// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> The response object of a token exchange invoke. </summary>
    public class TokenExchangeInvokeResponse
    {
        /// <summary> Initializes a new instance of TokenExchangeInvokeResponse. </summary>
        public TokenExchangeInvokeResponse()
        {
        }

        /// <summary>
        /// Gets or sets the id from the TokenExchangeInvokeRequest.
        /// </summary>
        /// <value>
        /// The id from the TokenExchangeInvokeRequest.
        /// </value>
        public string Id { get; set; }

        /// <summary> The connection name. </summary>
        public string ConnectionName { get; set; }
        /// <summary> The details of why the token exchange failed. </summary>
        public string FailureDetail { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Core.Models.Activity"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
