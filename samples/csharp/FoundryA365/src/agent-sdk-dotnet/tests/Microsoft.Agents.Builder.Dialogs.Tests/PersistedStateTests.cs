// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class PersistedStateTests
    {
        [Fact]
        public void PersistedState_ListRoundTrip()
        {
            var state = new TestState();
            state.State["block"] = new List<BaseBlock> { new SubBlock { Id = "1", Data = "Test" } };

            var json = ProtocolJsonSerializer.ToJson(state);

            var deserializedState = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.IsAssignableFrom<List<BaseBlock>>(deserializedState.State["block"]);
        }

        [Fact]
        public void PersistedState_ArrayRoundTrip()
        {
            var state = new TestState();
            state.State["block"] = new BaseBlock[] { new SubBlock { Id = "1", Data = "Test" } };

            var json = ProtocolJsonSerializer.ToJson(state);

            var deserializedState = ProtocolJsonSerializer.ToObject<TestState>(json);

            Assert.IsAssignableFrom<BaseBlock[]> (deserializedState.State["block"]);
        }

    }

    class TestState
    {
        public IDictionary<string, object> State { get; set; } = new Dictionary<string, object>();
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(SubBlock), "SUB")]
    public class BaseBlock
    {
        [JsonPropertyName("id")] public string Id { get; set; }
    }
    public class SubBlock : BaseBlock
    {
        [JsonPropertyName("data")] public string Data { get; set; }
    }
}
