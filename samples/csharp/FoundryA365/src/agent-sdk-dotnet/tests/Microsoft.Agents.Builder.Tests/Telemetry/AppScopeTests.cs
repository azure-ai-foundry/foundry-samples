// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Telemetry.App.Scopes;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.TestSupport;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class AppScopeTests : TelemetryScopeTestBase
    {
        private static ITurnContext CreateTurnContext(
            string type = "message",
            string channelId = "test-channel",
            string conversationId = "conv-1",
            string activityId = "act-1",
            int attachmentCount = 0)
        {
            var attachments = new List<Attachment>();
            for (int i = 0; i < attachmentCount; i++)
                attachments.Add(new Attachment { ContentType = "application/octet-stream" });

            return new TurnContext(new NotImplementedAdapter(), new Activity
            {
                Type = type,
                ChannelId = channelId,
                Id = activityId,
                Conversation = new ConversationAccount { Id = conversationId },
                Recipient = new ChannelAccount { Id = "bot-1" },
                From = new ChannelAccount { Id = "user-1" },
                Attachments = attachments
            });
        }

        #region ScopeOnTurn

        [Fact]
        public void ScopeOnTurn_CreatesActivity_WithCorrectName()
        {
            var ctx = CreateTurnContext();
            using var scope = new ScopeOnTurn(ctx);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.app.run", started.OperationName);
        }

        [Fact]
        public void ScopeOnTurn_Callback_SetsActivityMetadataTags()
        {
            var ctx = CreateTurnContext(type: "message", channelId: "msteams", conversationId: "conv-99", activityId: "act-42");
            var scope = new ScopeOnTurn(ctx);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("message", stopped.GetTagItem(TagNames.ActivityType));
            Assert.Equal("msteams", stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal("conv-99", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("act-42", stopped.GetTagItem(TagNames.ActivityId));
        }

        [Fact]
        public void ScopeOnTurn_Callback_SetsRouteTagsAfterShare()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeOnTurn(ctx);
            scope.Share(routeAuthorized: true, routeMatched: true);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteAuthorized));
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteMatched));
        }

        [Fact]
        public void ScopeOnTurn_Callback_RouteTagsAreNull_WhenShareNotCalled()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeOnTurn(ctx);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.RouteAuthorized));
            Assert.Null(stopped.GetTagItem(TagNames.RouteMatched));
        }

        [Fact]
        public void ScopeOnTurn_SetError_SetsErrorStatus()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeOnTurn(ctx);
            scope.SetError(new InvalidOperationException("turn error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("turn error", stopped.StatusDescription);
        }

        #endregion

        #region ScopeBeforeTurn

        [Fact]
        public void ScopeBeforeTurn_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeBeforeTurn();

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.app.before_turn", started.OperationName);
        }

        [Fact]
        public void ScopeBeforeTurn_SetError_SetsErrorStatus()
        {
            var scope = new ScopeBeforeTurn();
            scope.SetError(new InvalidOperationException("before-turn error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeAfterTurn

        [Fact]
        public void ScopeAfterTurn_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAfterTurn();

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.app.after_turn", started.OperationName);
        }

        [Fact]
        public void ScopeAfterTurn_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAfterTurn();
            scope.SetError(new InvalidOperationException("after-turn error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeRouteHandler

        [Fact]
        public void ScopeRouteHandler_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeRouteHandler(isInvoke: false, isAgentic: false);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.app.route_handler", started.OperationName);
        }

        [Fact]
        public void ScopeRouteHandler_Callback_SetsRouteIsInvokeTag()
        {
            var scope = new ScopeRouteHandler(isInvoke: true, isAgentic: false);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteIsInvoke));
            Assert.Equal(false, stopped.GetTagItem(TagNames.RouteIsAgentic));
        }

        [Fact]
        public void ScopeRouteHandler_Callback_SetsRouteIsAgenticTag()
        {
            var scope = new ScopeRouteHandler(isInvoke: false, isAgentic: true);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(false, stopped.GetTagItem(TagNames.RouteIsInvoke));
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteIsAgentic));
        }

        [Fact]
        public void ScopeRouteHandler_SetError_SetsErrorStatus()
        {
            var scope = new ScopeRouteHandler(isInvoke: false, isAgentic: false);
            scope.SetError(new InvalidOperationException("route error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeDownloadFiles

        [Fact]
        public void ScopeDownloadFiles_CreatesActivity_WithCorrectName()
        {
            var ctx = CreateTurnContext();
            using var scope = new ScopeDownloadFiles(ctx);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.app.download_files", started.OperationName);
        }

        [Fact]
        public void ScopeDownloadFiles_Callback_SetsAttachmentCountTag()
        {
            var ctx = CreateTurnContext(attachmentCount: 3);
            var scope = new ScopeDownloadFiles(ctx);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(3, stopped.GetTagItem(TagNames.AttachmentCount));
        }

        [Fact]
        public void ScopeDownloadFiles_Callback_SetsZeroAttachmentCount_WhenNoAttachments()
        {
            var ctx = CreateTurnContext(attachmentCount: 0);
            var scope = new ScopeDownloadFiles(ctx);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(0, stopped.GetTagItem(TagNames.AttachmentCount));
        }

        [Fact]
        public void ScopeDownloadFiles_SetError_SetsErrorStatus()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeDownloadFiles(ctx);
            scope.SetError(new InvalidOperationException("download error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}