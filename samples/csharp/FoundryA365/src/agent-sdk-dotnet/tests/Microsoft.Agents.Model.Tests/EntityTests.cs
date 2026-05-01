// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;
using Xunit;
using System.Text.Json;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Model.Tests
{
    public class EntityTests
    {
        [Fact]
        public void EntityInits()
        {
            var type = "entityType";

            var entity = new Entity(type);

            Assert.NotNull(entity);
            Assert.IsType<Entity>(entity);
            Assert.Equal(type, entity.Type);
        }

        [Fact]
        public void EntityInitsWithNoArgs()
        {
            var entity = new Entity();

            Assert.NotNull(entity);
            Assert.IsType<Entity>(entity);
        }

        [Fact]
        public void SetEntityAsTargetObject()
        {
            var entity = new Entity();
            Assert.Null(entity.Type);

            var entityType = "entity";
            var obj = new 
            {
                name = "Esper",
                eyes = "Brown",
                type = entityType
            };

            entity.SetAs(obj);
            var properties = entity.Properties;

            Assert.Equal(entityType, entity.Type);
            Assert.Equal(obj.name, properties["name"].ToString());
            Assert.Equal(obj.eyes, properties["eyes"].ToString());
        }

        [Fact]
        public void TestGetHashCode()
        {
            var hash = new Entity().GetHashCode();

            Assert.IsType<int>(hash);
        }

        [Theory]
        [ClassData(typeof(EntityToEntityData))]
        public void EntityEqualsAnotherEntity(Entity other, bool expected)
        {
            var entity = new Entity("color");
            var areEqual = entity.Equals(other);

            Assert.Equal(expected, areEqual);
        }

        [Theory]
        [ClassData(typeof(EntityToObjectData))]
        public void EntityEqualsObject(Entity entity, object obj, bool expected)
        {
            var areEqual = entity.Equals(obj);

            Assert.Equal(expected, areEqual);
        }

        [Fact]
        public void EntityUnknownTypeDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"unknown\", \"name\": \"name\"}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            Assert.IsType<Entity>(activity.Entities[0]);

            var entity = activity.Entities[0];
            Assert.IsAssignableFrom<Entity>(entity);
            Assert.Equal("unknown", entity.Type);
            Assert.NotEmpty(entity.Properties);
            Assert.True(entity.Properties.ContainsKey("name")); 
        }

        private class EntityToObjectData : IEnumerable<object[]>
        {
            public Entity Entity { get; set; } = new Entity("color");

            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { Entity, null, false };
                yield return new object[] { Entity, Entity, true };
                yield return new object[] { Entity, new JsonElement(), false };
                yield return new object[] { Entity, new Entity("color"), true };
                yield return new object[] { Entity, new Entity("flamingo"), false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class EntityToEntityData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new Entity("color"), true };
                yield return new object[] { new Entity("flamingo"), false };
                yield return new object[] { null, false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
