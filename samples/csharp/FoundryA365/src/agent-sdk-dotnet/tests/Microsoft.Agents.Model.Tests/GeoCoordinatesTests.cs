// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class GeoCoordinatesTests
    {
        [Fact]
        public void GeoCoordinatesInits()
        {
            var elevation = 14411.0;
            var latitude = 46.9;
            var longitude = 121.8;
            var type = "GeoCoordinates";
            var name = "Mt. Rainier";

            var geoCoordinate = new GeoCoordinates(elevation, latitude, longitude, type, name);

            Assert.NotNull(geoCoordinate);
            Assert.IsType<GeoCoordinates>(geoCoordinate);
            Assert.Equal(elevation, geoCoordinate.Elevation);
            Assert.Equal(latitude, geoCoordinate.Latitude);
            Assert.Equal(longitude, geoCoordinate.Longitude);
            Assert.Equal(type, geoCoordinate.Type);
            Assert.Equal(name, geoCoordinate.Name);
        }

        [Fact]
        public void GeoCoordinateInitsWithNoArgs()
        {
            var geoCoordinates = new GeoCoordinates();

            Assert.NotNull(geoCoordinates);
            Assert.IsType<GeoCoordinates>(geoCoordinates);
        }

        [Fact]
        public void GeoCoordinatesTypedDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"geoCoordinates\", \"name\": \"geoname\"}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            Assert.IsType<GeoCoordinates>(activity.Entities[0]);

            var geo = activity.Entities[0] as GeoCoordinates;
            Assert.Equal("geoname", geo.Name);
        }
    }
}
