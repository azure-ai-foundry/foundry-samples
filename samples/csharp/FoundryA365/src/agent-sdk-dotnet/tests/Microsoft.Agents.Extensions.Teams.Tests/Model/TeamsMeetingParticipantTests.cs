// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TeamsMeetingParticipantTests
    {
        [Fact]
        public void TeamsMeetingParticipantInits()
        {
            var user = new TeamsChannelAccount("joe@smith.com", "Joe", "Joe", "Smith", "joe@smith.com", "joePrincipalName");
            var conversation = new ConversationAccount(true);
            var meeting = new MeetingParticipantInfo("owner", true);

            var participant = new TeamsMeetingParticipant(user, conversation, meeting);

            Assert.NotNull(participant);
            Assert.IsType<TeamsMeetingParticipant>(participant);
            Assert.Equal(user, participant.User);
            Assert.Equal(conversation, participant.Conversation);
            Assert.Equal(meeting, participant.Meeting);
        }

        [Fact]
        public void TeamsMeetingParticipantInitsWithNoArgs()
        {
            var participant = new TeamsMeetingParticipant();

            Assert.NotNull(participant);
            Assert.IsType<TeamsMeetingParticipant>(participant);
        }
    }
}
