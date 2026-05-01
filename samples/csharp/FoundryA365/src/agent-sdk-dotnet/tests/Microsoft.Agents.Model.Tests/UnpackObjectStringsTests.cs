// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class UnpackObjectStringsTests
    {
        [Fact(Skip = "Needs to run separately")]
        public void ObjectWithEscapedStrings()
        {
            ProtocolJsonSerializer.SerializationOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            ProtocolJsonSerializer.UnpackObjectStrings = false;
            var incoming = "{\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[],\"channelData\":{\"name\":\"myName\"},\"value\":\"{\\\"a\\\":\\\"b\\\"}\",\"listenFor\":[],\"textHighlights\":[]}";
            var a = ProtocolJsonSerializer.ToObject<Activity>(incoming);
            Assert.True(a.Value is string);
            Assert.True(a.ChannelData is JsonElement);

            var outgoing = ProtocolJsonSerializer.ToJson(a);

            var parsedA = JsonSerializer.Deserialize<JsonElement>(incoming);
            var parsedB = JsonSerializer.Deserialize<JsonElement>(outgoing);

            var rawA = parsedA.GetRawText();
            var rawB = parsedB.GetRawText();

            var outActivity = new Activity() { ChannelData = new MyClass() { Name = "myName" }, Value = "{\"a\":\"b\"}" };
            var inJson = ProtocolJsonSerializer.ToJson(outActivity);
            var inActivity = ProtocolJsonSerializer.ToObject<IActivity>(inJson);

            Assert.Equal(rawA, rawB);
            Assert.Equal(rawA, inJson);
            Assert.Equal(rawA, JsonSerializer.Deserialize<JsonElement>(inJson).GetRawText());
        }

        [Fact(Skip = "Needs to run separately")]
        public void ValueObjectWithSimpleString()
        {
            ProtocolJsonSerializer.UnpackObjectStrings = false;
            var incoming = "{\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[],\"value\":\"just a string\",\"listenFor\":[],\"textHighlights\":[]}";
            var activityIn = ProtocolJsonSerializer.ToObject<Activity>(incoming);
            var jsonOut = ProtocolJsonSerializer.ToJson(activityIn);

            var outActivity = new Activity() { Value = "just a string" };
            var outActivityJson = ProtocolJsonSerializer.ToJson(outActivity);

            Assert.True(activityIn.Value is string);
            Assert.Equal("just a string", activityIn.Value);
            Assert.Equal(incoming, jsonOut);
            Assert.Equal(incoming, outActivityJson);
        }
    }
    class MyClass
    {
        public string Name { get; set; }
    }
}
