// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class MeetingsTests
    {
        [Fact]
        public async Task Test_OnStart()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingStart", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var ids = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) => {
                ext.Meetings.OnStart((context, _, _, _) =>
                {
                    ids.Add(context.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        [Fact]
        public async Task Test_OnEnd()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingEnd", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            var ids = new List<string>();
            app.RegisterExtension(extension, (ext) => {
                ext.Meetings.OnEnd((context, _, _, _) =>
                {
                    ids.Add(context.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        [Fact]
        public async Task Test_OnParticipantsJoin()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantJoin", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            var ids = new List<string>();
            app.RegisterExtension(extension, (ext) => {
                ext.Meetings.OnParticipantsJoin((context, _, _, _) =>
                {
                    ids.Add(context.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        [Fact]
        public async Task Test_OnParticipantsLeave()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantLeave", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var extension = new TeamsAgentExtension(app);
            var ids = new List<string>();

            app.RegisterExtension(extension, (ext) => {
                ext.Meetings.OnParticipantsLeave((context, _, _, _) =>
                {
                    ids.Add(context.Activity.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        private static ITurnContext[] CreateMeetingTurnContext(string activityName, ChannelAdapter adapter)
        {
            return new TurnContext[]
            {
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Event,
                    ChannelId = Channels.Msteams,
                    Name = activityName,
                    Id = "test.id",
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Event,
                    ChannelId = Channels.Msteams,
                    Name = "fake.name",
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Invoke,
                    ChannelId = Channels.Msteams,
                    Name = activityName,
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Event,
                    ChannelId = Channels.Webchat,
                    Name = activityName,
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
            };
        }
    }
}
