// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class TypeInfoTests
    {
        [Fact]
        public async Task DoesHandleNewtonsoftTypeInfo()
        {
            var record = "{\"$type\": \"Microsoft.Agents.Storage.Tests.WelcomeUserState, Microsoft.Agents.Storage.Tests\", \"DidBotWelcomeUser\": true}";
            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>
            {
                ["welcomState"] = JsonObject.Parse(record).AsObject()
            });

            var items = await storage.ReadAsync(new[] { "welcomState" }, default);
            Assert.True(items.ContainsKey("welcomState"));
            Assert.IsType<WelcomeUserState>(items["welcomState"]);
            Assert.True(((WelcomeUserState)items["welcomState"]).DidBotWelcomeUser);
        }

        [Fact]
        public async Task DoesHandleTypeInfo()
        {
            var record = "{\"$type\": \"Microsoft.Agents.Storage.Tests.WelcomeUserState\", \"$typeAssembly\": \"Microsoft.Agents.Storage.Tests\", \"DidBotWelcomeUser\": true}";
            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>
            {
                ["welcomState"] = JsonObject.Parse(record).AsObject()
            });

            var items = await storage.ReadAsync(new[] { "welcomState" }, default);
            Assert.True(items.ContainsKey("welcomState"));
            Assert.IsType<WelcomeUserState>(items["welcomState"]);
            Assert.True(((WelcomeUserState)items["welcomState"]).DidBotWelcomeUser);
        }

        [Fact]
        public async Task DoesHandleMissingAssemblyValue()
        {
            var record = "{\"$type\": \"Microsoft.Agents.Storage.Tests.WelcomeUserState\", \"DidBotWelcomeUser\": true}";
            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>
            {
                ["welcomState"] = JsonObject.Parse(record).AsObject()
            });

            var items = await storage.ReadAsync(new[] { "welcomState" }, default);
            Assert.True(items.ContainsKey("welcomState"));
            Assert.IsType<JsonObject>(items["welcomState"]);
        }

        [Fact]
        public async Task DoesHandleMissingTypeInfo()
        {
            var record = "{\"DidBotWelcomeUser\": true}";
            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>
            {
                ["welcomState"] = JsonObject.Parse(record).AsObject()
            });

            var items = await storage.ReadAsync(new[] { "welcomState" }, default);
            Assert.True(items.ContainsKey("welcomState"));
            Assert.IsType<JsonObject>(items["welcomState"]);
        }
    }

    public class WelcomeUserState
    {
        public bool DidBotWelcomeUser { get; set; }
    }
}
