// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class TeamsChannelDataTests
    {
        [Fact]
        public void TeamsChannelDataInits()
        {
            var channel = new ChannelInfo("general", "General");
            var eventType = "eventType";
            var team = new TeamInfo("supportEngineers", "Support Engineers");
            var notification = new NotificationInfo(true);
            var tenant = new TenantInfo("uniqueTenantId");
            var meeting = new TeamsMeetingInfo("BFSE Stand Up");
            var settings = new TeamsChannelDataSettings(channel);
            var onBehalfOf = new List<OnBehalfOf>()
            {
                new OnBehalfOf()
                {
                    DisplayName = "onBehalfOfTest",
                    ItemId = 0,
                    MentionType = "person",
                    Mri = Guid.NewGuid().ToString()
                }
            };
            var channelData = new TeamsChannelData(channel, eventType, team, notification, tenant, onBehalfOf)
            {
                Meeting = meeting,
                Settings = settings
            };

            Assert.NotNull(channelData);
            Assert.IsType<TeamsChannelData>(channelData);
            Assert.Equal(channel, channelData.Channel);
            Assert.Equal(eventType, channelData.EventType);
            Assert.Equal(team, channelData.Team);
            Assert.Equal(notification, channelData.Notification);
            Assert.Equal(tenant, channelData.Tenant);
            Assert.Equal(settings, channelData.Settings);
            Assert.Equal(channel, channelData.Settings.SelectedChannel);
            Assert.Equal(onBehalfOf, channelData.OnBehalfOf);
        }

        [Fact]
        public void TeamsChannelDataInitsWithNoArgs()
        {
            var channelData = new TeamsChannelData();

            Assert.NotNull(channelData);
            Assert.IsType<TeamsChannelData>(channelData);
        }

        [Fact]
        public void GetAadGroupId()
        {
            // Arrange
            const string AadGroupId = "teamGroup123";
            var activity = (IActivity)new Activity { ChannelData = new TeamsChannelData { Team = new TeamInfo { AadGroupId = AadGroupId } } };

            // Act
            var channelData = activity.GetChannelData<TeamsChannelData>();

            // Assert
            Assert.Equal(AadGroupId, channelData.Team.AadGroupId);
        }

        [Fact]
        public void AdditionalProperties_ExtraChannelDataFields()
        {
            // Arrange
            const string TestKey = "thekey";
            const string TestValue = "the test value";

            // TeamsChannelData with additional property.
            var json = "{\"team\": {\"aadGroupId\": \"id\"}, \"thekey\": \"the test value\"}";

            // Act
            var asTeamsChannelData = ProtocolJsonSerializer.ToObject<TeamsChannelData>(json);

            // Assert
            Assert.True(asTeamsChannelData.Properties.ContainsKey(TestKey));
            Assert.Equal(TestValue, asTeamsChannelData.Properties[TestKey].ToString());
        }

        [Fact]
        public void Activity_TeamsChannelData()
        {
            const string TestKey = "thekey";
            const string TestValue = "the test value";

            var json = "{\"type\": \"message\", \"channelData\": {\"tenant\": {\"id\":\"tenantid\"}, \"meeting\": {\"id\":\"meetingid\"}, \"team\": {\"id\": \"id\", \"name\": \"name\", \"aadGroupId\": \"aadGroupId\"}, \"thekey\": \"the test value\"}}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);
            var channelData = activity.GetChannelData<TeamsChannelData>();

            Assert.IsType<TeamsChannelData>(channelData);
            Assert.Equal("id", channelData.Team.Id);
            Assert.Equal("name", channelData.Team.Name);
            Assert.Equal("aadGroupId", channelData.Team.AadGroupId);
            Assert.Equal("meetingid", channelData.Meeting.Id);
            Assert.Equal("tenantid", channelData.Tenant.Id);
            Assert.True(channelData.Properties.ContainsKey(TestKey));
            Assert.Equal(TestValue, channelData.Properties[TestKey].ToString());
        }

        [Fact]
        public void TeamsChannelDataRoundTrip()
        {
            var teamsChannelData = new TeamsChannelData()
            {
                Channel = new ChannelInfo() { Id = "channel_id", Name = "channel_name", Type = "channel_type" },
                EventType = "eventType",
                Team = new TeamInfo() { Id = "team_id", Name = "team_name", AadGroupId = "aadgroupid_id" },
                Notification = new NotificationInfo() { Alert = true, AlertInMeeting = true, ExternalResourceUrl = "resourceUrl" },
                Tenant = new TenantInfo() { Id = "tenant_id" },
                Meeting = new TeamsMeetingInfo() { Id = "meeting_id" },
                Settings = new TeamsChannelDataSettings() { SelectedChannel = new ChannelInfo() { Id = "channel_id", Name = "channel_name", Type = "channel_type" }, Properties = ProtocolJsonSerializer.ToJsonElements(new { prop1 = "prop1"}) },
                OnBehalfOf = [ new OnBehalfOf() {  DisplayName = "displayName", ItemId = 1, MentionType = "mentionType", Mri = "mri"}],
                Properties = ProtocolJsonSerializer.ToJsonElements(new { prop1 = "root_prop1" })
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(teamsChannelData);

            // Out
            var json = ProtocolJsonSerializer.ToJson(teamsChannelData);
            Assert.Equal(goodJson, json);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<TeamsChannelData>(json);
            json = ProtocolJsonSerializer.ToJson(inObj);
            Assert.Equal(goodJson, json);
        }
    }
}
