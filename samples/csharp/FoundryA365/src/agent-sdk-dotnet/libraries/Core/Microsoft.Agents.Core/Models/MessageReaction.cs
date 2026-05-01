// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Message reaction object. </summary>
    public class MessageReaction
    {
        public MessageReaction() { }

        /// <summary> Initializes a new instance of MessageReaction. </summary>
        /// <param name="type"> Message reaction types. </param>
        public MessageReaction(string type = default)
        {
            Type = type;
        }

        /// <summary> Message reaction types. </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Core.Models.MessageReaction"/> type but that
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
