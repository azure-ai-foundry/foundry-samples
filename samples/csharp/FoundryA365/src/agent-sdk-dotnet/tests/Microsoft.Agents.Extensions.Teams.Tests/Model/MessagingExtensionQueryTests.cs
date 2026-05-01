// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessagingExtensionQueryTests
    {
        [Fact]
        public void MessagingExtensionQueryInits()
        {
            var commandId = "commandId123";
            var parameters = new List<MessagingExtensionParameter>() { new MessagingExtensionParameter("pandaCount", 5) };
            var queryOptions = new MessagingExtensionQueryOptions(0, 1);
            var state = "secureAuthStateValue123";

            var msgExtQuery = new MessagingExtensionQuery(commandId, parameters, queryOptions, state);

            Assert.NotNull(msgExtQuery);
            Assert.IsType<MessagingExtensionQuery>(msgExtQuery);
            Assert.Equal(commandId, msgExtQuery.CommandId);
            Assert.Equal(parameters, msgExtQuery.Parameters);
            Assert.Equal(queryOptions, msgExtQuery.QueryOptions);
            Assert.Equal(state, msgExtQuery.State);
        }

        [Fact]
        public void MessagingExtensionQueryInitsWithNoArgs()
        {
            var msgExtQuery = new MessagingExtensionQuery();

            Assert.NotNull(msgExtQuery);
            Assert.IsType<MessagingExtensionQuery>(msgExtQuery);
        }

        [Fact]
        public void MessagingExtensionActionRoundTrip()
        {
            var msgExtQuery = new MessagingExtensionQuery()
            {
                CommandId = "commandId",
                Parameters = [
                        new MessagingExtensionParameter()
                        {
                            Name = "name1",
                            Value = new { param1 = "value1" }
                        },
                        new MessagingExtensionParameter()
                        {
                            Name = "name2",
                            Value = new { param1 = "value2" }
                        }
                    ],
                QueryOptions = new MessagingExtensionQueryOptions()
                {
                    Skip = 1,
                    Count = 10
                },
                State = "state"
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(msgExtQuery);

            // Out
            var json = ProtocolJsonSerializer.ToJson(msgExtQuery);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<MessagingExtensionQuery>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }

    }
}
