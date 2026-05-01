// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ThingTests
    {
        [Fact]
        public void ThingInits()
        {
            var type = "thing";
            var name = "name";

            var thing = new Thing(type, name);

            Assert.NotNull(thing);
            Assert.IsType<Thing>(thing);
            Assert.Equal(type, thing.Type);
            Assert.Equal(name, thing.Name);
        }
        
        [Fact]
        public void ThingInitsWithNoArgs()
        {
            var thing = new Thing();

            Assert.NotNull(thing);
            Assert.IsType<Thing>(thing);
        }

        [Fact]
        public void ThingTypedDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"thing\", \"name\": \"thingname\"}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            Assert.IsType<Thing>(activity.Entities[0]);

            var thing = activity.Entities[0] as Thing;
            Assert.Equal("thingname", thing.Name);
        }
    }
}
