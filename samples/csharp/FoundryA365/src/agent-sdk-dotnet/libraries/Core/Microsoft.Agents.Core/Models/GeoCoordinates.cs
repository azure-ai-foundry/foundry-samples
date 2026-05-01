// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> GeoCoordinates (entity type: "https://schema.org/GeoCoordinates"). </summary>
    public class GeoCoordinates : Entity
    {
        /// <summary> Initializes a new instance of GeoCoordinates. </summary>
        public GeoCoordinates()
            :this(type: EntityTypes.GeoCoordinates)
        {
        }

        /// <summary> Initializes a new instance of GeoCoordinates. </summary>
        /// <param name="type"> Type of this entity (RFC 3987 IRI). </param>
        /// <param name="elevation"> Elevation of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </param>
        /// <param name="latitude"> Latitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </param>
        /// <param name="longitude"> Longitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </param>
        /// <param name="name"> The name of the thing. </param>
        public GeoCoordinates(double? elevation = default, double? latitude = default, double? longitude = default, string type = "GeoCoordinates", string name = default)
        {
            Elevation = elevation;
            Latitude = latitude;
            Longitude = longitude;
            Name = name;
            Type = type;
        }

        /// <summary> Initializes a new instance of GeoCoordinates. </summary>
        /// <param name="elevation"> Elevation of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </param>
        /// <param name="latitude"> Latitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </param>
        /// <param name="longitude"> Longitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </param>
        /// <param name="name"> The name of the thing. </param>
        public GeoCoordinates(double? elevation, double? latitude, double? longitude, string name) : this(elevation, latitude, longitude, "GeoCoordinates", name)
        {
        }

        /// <summary> Elevation of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </summary>
        public double? Elevation { get; set; }
        /// <summary> Latitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </summary>
        public double? Latitude { get; set; }
        /// <summary> Longitude of the location [WGS 84](https://en.wikipedia.org/wiki/World_Geodetic_System). </summary>
        public double? Longitude { get; set; }
        /// <summary> The name of the thing. </summary>
        public string Name { get; set; }
    }
}
