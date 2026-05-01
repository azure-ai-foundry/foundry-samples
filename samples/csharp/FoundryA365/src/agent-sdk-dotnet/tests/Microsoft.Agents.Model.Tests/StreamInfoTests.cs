using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class StreamInfoTests
    {
        [Fact]
        public void StreamInfoRoundTrip()
        {
            var outStreamInfo = new StreamInfo()
            {
                StreamId = "id",
                StreamType = "streamType",
                StreamResult = "result",
                StreamSequence = 1
            };

            var json = ProtocolJsonSerializer.ToJson(outStreamInfo);
            var inEntity = ProtocolJsonSerializer.ToObject<Entity>(json);

            Assert.IsAssignableFrom<StreamInfo>(inEntity);

            var inStreamInfo = inEntity as StreamInfo;
            Assert.Equal(outStreamInfo.Type, inStreamInfo.Type);
            Assert.Equal(outStreamInfo.StreamType, inStreamInfo.StreamType);
            Assert.Equal(outStreamInfo.StreamId, inStreamInfo.StreamId);
            Assert.Equal(outStreamInfo.StreamResult, inStreamInfo.StreamResult);
            Assert.Equal(outStreamInfo.StreamSequence, inStreamInfo.StreamSequence);
        }

        [Fact]
        public void StreamInfoTypedDeserialize()
        {
            var json = "{\"entities\": [{\"type\": \"streaminfo\", \"streamType\": \"streamType\", \"streamId\": \"id\"}]}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.NotNull(activity.Entities);
            Assert.NotEmpty(activity.Entities);
            Assert.IsType<StreamInfo>(activity.Entities[0]);
            var streamInfo = activity.Entities[0] as StreamInfo;
            Assert.Equal("streaminfo", streamInfo.Type);
            Assert.Equal("streamType", streamInfo.StreamType);
            Assert.Equal("id", streamInfo.StreamId);
        }

        [Fact]
        public void StreamInfoTypedSerialize()
        {
            var activity = new Activity()
            {
                Type = ActivityTypes.Typing,
                Entities = [new StreamInfo() { StreamType = "streamType", StreamId = "streamId", StreamResult = "streamResult", StreamSequence = 1 }]
            };

            var json = ProtocolJsonSerializer.ToJson(activity);
#if SKIP_EMPTY_LISTS
            Assert.Equal("{\"type\":\"typing\",\"entities\":[{\"streamId\":\"streamId\",\"streamType\":\"streamType\",\"streamSequence\":1,\"streamResult\":\"streamResult\",\"type\":\"streaminfo\"}]}", json);
#else
            Assert.Equal("{\"type\":\"typing\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[{\"streamId\":\"streamId\",\"streamType\":\"streamType\",\"streamSequence\":1,\"streamResult\":\"streamResult\",\"type\":\"streaminfo\"}],\"listenFor\":[],\"textHighlights\":[]}", json);
#endif
        }
    }
}
