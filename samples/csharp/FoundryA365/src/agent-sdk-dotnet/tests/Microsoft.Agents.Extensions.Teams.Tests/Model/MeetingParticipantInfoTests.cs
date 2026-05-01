// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MeetingParticipantInfoTests
    {
        [Fact]
        public void MeetingParticipantInfoInits()
        {
            var role = "organizer";
            var inMeeting = true;

            var meetingParticipantInfo = new MeetingParticipantInfo(role, inMeeting);

            Assert.NotNull(meetingParticipantInfo);
            Assert.IsType<MeetingParticipantInfo>(meetingParticipantInfo);
            Assert.Equal(role, meetingParticipantInfo.Role);
            Assert.Equal(inMeeting, meetingParticipantInfo.InMeeting);
        }

        [Fact]
        public void MeetingParticipantInfoInitsWithNoArgs()
        {
            var meetingParticipantInfo = new MeetingParticipantInfo();

            Assert.NotNull(meetingParticipantInfo);
            Assert.IsType<MeetingParticipantInfo>(meetingParticipantInfo);
        }
    }
}
