// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    public class TeamsActivityExtensionsTests
    {
        [Fact]
        public void TeamsGetSelectedChannelId_ShouldReturnChannelId()
        {
            IActivity activity = new Activity { ChannelData = new TeamsChannelData { Settings = new TeamsChannelDataSettings { SelectedChannel = new ChannelInfo("channel123") } } };

            var channelId = activity.TeamsGetSelectedChannelId();

            Assert.Equal("channel123", channelId);
        }

        [Fact]
        public void TeamsGetSelectedChannelId_ShouldReturnNullOnNullSettings()
        {
            IActivity activity = new Activity { ChannelData = new TeamsChannelData { Settings = null } };

            var channelId = activity.TeamsGetSelectedChannelId();

            Assert.Null(channelId);
        }

        [Fact]
        public void TeamsGetMeetingInfo_ShouldReturnMeetingId()
        {
            var activity = new Activity { ChannelData = new TeamsChannelData { Meeting = new TeamsMeetingInfo { Id = "meeting123" } } };

            var meetingId = activity.TeamsGetMeetingInfo().Id;

            Assert.Equal("meeting123", meetingId);
        }

        [Fact]
        public void TeamsGetChannelId_ShouldReturnChannelId()
        {
            IActivity activity = new Activity { ChannelData = new TeamsChannelData { Channel = new ChannelInfo { Id = "channel123" } } };

            var channelId = activity.TeamsGetChannelId();

            Assert.Equal("channel123", channelId);
        }

        [Fact]
        public void TeamsGetChannelId_ShouldReturnNullOnNullChannel()
        {
            IActivity activity = new Activity { ChannelData = new TeamsChannelData { Channel = null } };

            var channelId = activity.TeamsGetChannelId();

            Assert.Null(channelId);
        }

        [Fact]
        public void TeamsGetTeamInfo_ShouldReturnTeamId()
        {
            IActivity activity = new Activity { ChannelData = new TeamsChannelData { Team = new TeamInfo { Id = "team1234" } } };

            var teamId = activity.TeamsGetTeamInfo().Id;

            Assert.Equal("team1234", teamId);
        }

        [Fact]
        public void TeamsGetTeamInfo_ShouldReturnTeamIdFromTypedActivity()
        {
            IMessageActivity activity = new Activity { ChannelData = new TeamsChannelData { Team = new TeamInfo { Id = "team123" } } };

            var teamId = activity.TeamsGetTeamInfo().Id;

            Assert.Equal("team123", teamId);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldConfigureAlert()
        {
            var activity = new Activity { };

            activity.TeamsNotifyUser();

            Assert.Equal(true, ((TeamsChannelData)activity.ChannelData).Notification.Alert);
            Assert.Equal(false, ((TeamsChannelData)activity.ChannelData).Notification.AlertInMeeting);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldConfigureAlertInMeeting()
        {
            var activity = new Activity { };

            activity.TeamsNotifyUser(alertInMeeting: true);

            Assert.Equal(true, ((TeamsChannelData)activity.ChannelData).Notification.AlertInMeeting);
            Assert.Equal(false, ((TeamsChannelData)activity.ChannelData).Notification.Alert);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldUseExternalResourceUrl()
        {
            string resourceUrl = "https://microsoft.com";

            var activity = new Activity { };

            activity.TeamsNotifyUser(false, externalResourceUrl: resourceUrl);

            Assert.Equal(resourceUrl, ((TeamsChannelData)activity.ChannelData).Notification.ExternalResourceUrl);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldNotOverrideExistingChannelData()
        {
            var activity = new Activity { ChannelData = new TeamsChannelData { Team = new TeamInfo { Id = "team123" } } };

            activity.TeamsNotifyUser();

            Assert.True(((TeamsChannelData)activity.ChannelData).Notification.Alert);
            Assert.Equal("team123", ((TeamsChannelData)activity.ChannelData).Team.Id);
        }

        [Fact]
        public void TeamsGetTeamOnBehalfOf_ShouldReturnOnBehalfOf()
        {
            var onBehalfOf = new OnBehalfOf
            {
                DisplayName = "TestOnBehalfOf",
                ItemId = 0,
                MentionType = "person",
                Mri = Guid.NewGuid().ToString()
            };

            IActivity activity = new Activity { ChannelData = new TeamsChannelData(onBehalfOf: new List<OnBehalfOf> { onBehalfOf }) };

            var onBehalfOfList = activity.TeamsGetTeamOnBehalfOf();

            Assert.Single(onBehalfOfList);
            Assert.Equal("TestOnBehalfOf", onBehalfOfList[0].DisplayName);
        }
    }
}
