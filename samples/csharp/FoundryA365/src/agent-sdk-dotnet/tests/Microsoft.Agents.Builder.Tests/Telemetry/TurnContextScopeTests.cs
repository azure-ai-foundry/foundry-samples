// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Telemetry.TurnContext;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.TestSupport;
using System;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class TurnContextScopeTests : TelemetryScopeTestBase
    {
        private static ITurnContext CreateTurnContext(
            string conversationId = "conv-1",
            string activityId = "act-1")
        {
            return new TurnContext(new NotImplementedAdapter(), new Activity
            {
                Type = "message",
                ChannelId = "test-channel",
                Id = activityId,
                Conversation = new ConversationAccount { Id = conversationId },
                Recipient = new ChannelAccount { Id = "bot-1" },
                From = new ChannelAccount { Id = "user-1" }
            });
        }

        #region ScopeSendActivities

        [Fact]
        public void ScopeSendActivities_CreatesActivity_WithCorrectName()
        {
            var ctx = CreateTurnContext();
            using var scope = new ScopeSendActivities(ctx);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.turn.send_activities", started.OperationName);
        }

        [Fact]
        public void ScopeSendActivities_Callback_SetsConversationIdTag()
        {
            var ctx = CreateTurnContext(conversationId: "conv-send-123");
            var scope = new ScopeSendActivities(ctx);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-send-123", stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void ScopeSendActivities_SetError_SetsErrorStatus()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeSendActivities(ctx);
            scope.SetError(new InvalidOperationException("send error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}