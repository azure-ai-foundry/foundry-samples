// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class SurfaceTests
    {
        [Fact]
        public void Surface_In_TargetedMeetingNotificationValue()
        {
            //
            // Deserialize
            //

            // TargetedMeetingNotificationValue json
            // {"surfaces":[{"contentType":"task","surface":"meetingStage"},{"tabEntityId":"id","surface":"meetingTabIcon"}]}
            var json = "{\"surfaces\":[{\"contentType\":\"task\",\"surface\":\"meetingStage\"},{\"tabEntityId\":\"id\",\"surface\":\"meetingTabIcon\"}]}";
            var json_compat = "{\"surfaces\": [{\"type\": \"MeetingStage\", \"contentType\": \"Task\"}, {\"type\": \"MeetingTabIcon\", \"tabEntityId\": \"id\"}]}";

            // Act
            var obj = ProtocolJsonSerializer.ToObject<TargetedMeetingNotificationValue>(json);
            Assert.NotNull(obj);

            // first Surface
            Assert.IsAssignableFrom<MeetingStageSurface<TaskModuleContinueResponse>>(obj.Surfaces[0]);

            // second Surface
            Assert.IsAssignableFrom<MeetingTabIconSurface>(obj.Surfaces[1]);
            Assert.Equal("id", ((MeetingTabIconSurface)obj.Surfaces[1]).TabEntityId);

            // Again with compat variant
            obj = ProtocolJsonSerializer.ToObject<TargetedMeetingNotificationValue>(json_compat);
            Assert.NotNull(obj);

            // first Surface
            Assert.IsAssignableFrom<MeetingStageSurface<TaskModuleContinueResponse>>(obj.Surfaces[0]);

            // second Surface
            Assert.IsAssignableFrom<MeetingTabIconSurface>(obj.Surfaces[1]);
            Assert.Equal("id", ((MeetingTabIconSurface)obj.Surfaces[1]).TabEntityId);

            //
            // Serialize
            //
            var to_json = ProtocolJsonSerializer.ToJson(obj);
            Assert.Equal(json, to_json);
        }
    }
}
