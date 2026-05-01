// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Entities carry metadata about an activity or conversation. Each entity's meaning and shape is defined by 
    /// the type field. Additional type-specific fields sit as peers to the type field.
    ///
    /// Some Microsoft entities may have a preexisting field called type. Parties integrating these entities 
    /// into the activity entity format are advised to define field-level mapping to resolve conflicts with the type 
    /// field name and other incompatibilities with serialization requirement A2001 as part of the IRI defining the 
    /// entity type.
    /// 
    /// Frequently, entities used within Activity Protocol are also expressed elsewhere using JSON-LD[17]. The entity 
    /// format is designed to be compatible with JSON-LD contexts, but does not require senders or receivers to 
    /// implement JSON-LD to successfully process an entity.
    /// </summary>
    public class Entity
    {
        /// <summary> Initializes a new instance of Entity. </summary>
        public Entity()
        {
        }

        /// <summary> Initializes a new instance of Entity. </summary>
        /// <param name="type"> Type of this entity (RFC 3987 IRI). </param>
        public Entity(string type)
        {
            Type = type;
        }

        /// <summary> Type of this entity (RFC 3987 IRI). </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Core.Models.Entity"/> type but that
        /// might appear in the REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">The other object to compare against.</param>
        /// <returns>true if the current object is equal to the other parameter, otherwise false.</returns>
        public bool Equals(Entity other)
        {
            if (other == null)
            {
                return false;
            }

            // This is serialization independent. Using JSON to compare objects.
            return JsonSerializer.Serialize(this).Equals(JsonSerializer.Serialize(other), StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The other object to compare against.</param>
        /// <returns>true if the current object is equal to the obj parameter, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals(obj as Entity);
        }

        /// <summary>
        /// Hash function that generates a hash code for the current object.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
