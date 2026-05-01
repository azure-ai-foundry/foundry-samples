// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A response that includes a user token. </summary>
    public class TokenResponse
    {
        public TokenResponse() { }

        /// <summary> Initializes a new instance of TokenResponse. </summary>
        /// <param name="channelId"> The channelId of the TokenResponse. </param>
        /// <param name="connectionName"> The connection name. </param>
        /// <param name="token"> The user token. </param>
        /// <param name="expiration"> Expiration for the token, in ISO 8601 format (e.g. "2007-04-05T14:30Z"). </param>
        public TokenResponse(string channelId = default, string connectionName = default, string token = default, DateTime expiration = default)
        {
            ChannelId = channelId;
            ConnectionName = connectionName;
            Token = token;

            if (expiration != default) // do not set the expiration if the default value is passed. If the timezone is + UTC it will cause a fault when assigned as an offset.
                Expiration = expiration;
        }

        /// <summary> The channelId of the TokenResponse. </summary>
        public string ChannelId { get; set; }
        /// <summary> The connection name. </summary>
        public string ConnectionName { get; set; }
        /// <summary> The user token. </summary>
        public string Token { get; set; }
        /// <summary> Expiration for the token, in ISO 8601 format (e.g. "2007-04-05T14:30Z"). </summary>
        public DateTimeOffset? Expiration { get; set; }

        public bool IsExchangeable { get; set; }

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
