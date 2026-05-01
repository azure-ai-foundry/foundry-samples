// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    /// <summary>
    /// Entity schema validation tests to ensure that serilization and deserialization work as expected.
    /// </summary>
    public class EntitySchemaValidationTest
    {
        /// <summary>
        /// Ensures that <see cref="GeoCoordinates"/> class can be serialized and deserialized properly.
        /// </summary>
        [Fact]
        public void EntityTests_GeoCoordinatesSerializationDeserializationTest()
        {
            GeoCoordinates geoCoordinates = new GeoCoordinates()
            {
                Latitude = 22,
                Elevation = 23,
            };

            Assert.Equal("GeoCoordinates", geoCoordinates.Type);
            string serialized = ProtocolJsonSerializer.ToJson(geoCoordinates);

            Entity deserializedEntity = ProtocolJsonSerializer.ToObject<Entity>(serialized);
            Assert.Equal(deserializedEntity.Type, geoCoordinates.Type);
            Assert.IsType<GeoCoordinates>(deserializedEntity);
        }

        /// <summary>
        /// Ensures that <see cref="Mention"/> class can be serialized and deserialized properly.
        /// </summary>
        [Fact]
        public void EntityTests_MentionSerializationDeserializationTest()
        {
            Mention mentionEntity = new Mention()
            {
                Text = "TESTTEST",
            };

            Assert.Equal("mention", mentionEntity.Type);
            string serialized = ProtocolJsonSerializer.ToJson(mentionEntity);

            Entity deserializedEntity = ProtocolJsonSerializer.ToObject<Entity>(serialized);
            Assert.Equal(deserializedEntity.Type, mentionEntity.Type);
            Assert.IsType<Mention>(deserializedEntity);
        }

        /// <summary>
        /// Ensures that <see cref="Place"/> class can be serialized and deserialized properly.
        /// </summary>
        [Fact]
        public void EntityTests_PlaceSerializationDeserializationTest()
        {
            Place placeEntity = new Place()
            {
                Name = "TESTTEST",
            };

            Assert.Equal("Place", placeEntity.Type);
            string serialized = ProtocolJsonSerializer.ToJson(placeEntity);

            Entity deserializedEntity = ProtocolJsonSerializer.ToObject<Entity>(serialized);
            Assert.Equal(deserializedEntity.Type, placeEntity.Type);
            Assert.IsType<Place>(deserializedEntity);
        }
    }
}
