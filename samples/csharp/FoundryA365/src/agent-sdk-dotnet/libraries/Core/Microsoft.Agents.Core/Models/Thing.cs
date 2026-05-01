// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Thing (entity type: "https://schema.org/Thing"). </summary>
    public class Thing : Entity
    {
        public Thing() { }

        /// <summary> Initializes a new instance of Thing. </summary>
        /// <param name="type"> Type of this entity (RFC 3987 IRI). </param>
        /// <param name="name"> The name of the thing. </param>
        public Thing(string type = default, string name = default)
        {
            Name = name;
            Type = type;
        }

        /// <summary> The name of the thing. </summary>
        public string Name { get; set; }
    }
}
