// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TeamsEntityTests
    {
        [Fact]
        public void ClientInfoDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"clientInfo\", \"locale\": \"en-us\", \"country\": \"us\", \"platform\": \"windows\"}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            var entity = activity.Entities[0] as ClientInfo;
            Assert.IsType<ClientInfo>(entity, exactMatch: false);
            Assert.Equal("clientInfo", entity.Type);
            Assert.Equal("en-us", entity.Locale);
            Assert.Equal("us", entity.Country);
            Assert.Equal("windows", entity.Platform);
        }
    }
}
