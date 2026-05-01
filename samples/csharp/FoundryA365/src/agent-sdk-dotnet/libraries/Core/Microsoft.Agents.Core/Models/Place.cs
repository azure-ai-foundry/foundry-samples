// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Place (entity type: "https://schema.org/Place"). </summary>
    public class Place : Entity
    {
        /// <summary> Initializes a new instance of Place. </summary>
        public Place() : base(EntityTypes.Place)
        {
        }

        /// <summary> Initializes a new instance of Place. </summary>
        /// <param name="type"> Type of this entity (RFC 3987 IRI). </param>
        /// <param name="address"> Address of the place (may be `string` or complex object of type `PostalAddress`). </param>
        /// <param name="geo"> Geo coordinates of the place (may be complex object of type `GeoCoordinates` or `GeoShape`). </param>
        /// <param name="hasMap"> Map to the place (may be `string` (URL) or complex object of type `Map`). </param>
        /// <param name="name"> The name of the thing. </param>
        public Place(object address = default, object geo = default, object hasMap = default, string type = EntityTypes.Place, string name = default)
        {
            Address = address;
            Geo = geo;
            HasMap = hasMap;
            Name = name;
            Type = type;
        }

        /// <summary> Address of the place (may be `string` or complex object of type `PostalAddress`). </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Address { get; set; }
        /// <summary> Geo coordinates of the place (may be complex object of type `GeoCoordinates` or `GeoShape`). </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Geo { get; set; }
        /// <summary> Map to the place (may be `string` (URL) or complex object of type `Map`). </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object HasMap { get; set; }
        /// <summary> The name of the thing. </summary>
        public string Name { get; set; }
    }
}
