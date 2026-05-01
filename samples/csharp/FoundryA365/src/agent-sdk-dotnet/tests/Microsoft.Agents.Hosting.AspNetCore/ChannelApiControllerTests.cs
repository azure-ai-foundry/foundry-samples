// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ChannelApiControllerTests
    {
        private static Activity _activity => new() { Id = "123", Text = "test" };

        [Fact]
        public async Task SendToConversationAsync_ShouldCallHandlerWithActivity()
        {
            var record = UseRecord(_activity);
            var conversationId = Guid.NewGuid().ToString();

            record.Handler.Setup(e => e.OnSendToConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);

            var result = await record.Controller.SendToConversationAsync(conversationId) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            record.VerifyMocks();
        }

        [Fact]
        public async Task SendToConversationAsync_ShouldNotCallHandlerWithoutActivity()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnSendToConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);

            var result = await record.Controller.SendToConversationAsync("");

            Assert.Null(result);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldCallHandlerWithActivity()
        {
            var record = UseRecord(_activity);
            var conversationId = Guid.NewGuid().ToString();

            record.Handler.Setup(e => e.OnReplyToActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);

            var result = await record.Controller.ReplyToActivityAsync(conversationId, _activity.Id) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            record.VerifyMocks();
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldNotCallHandlerWithoutActivity()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnReplyToActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);

            var result = await record.Controller.ReplyToActivityAsync("", "");

            Assert.Null(result);
            record.VerifyMocks();
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldCallHandlerWithActivity()
        {
            var record = UseRecord(_activity);
            var conversationId = Guid.NewGuid().ToString();

            record.Handler.Setup(e => e.OnUpdateActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);

            var result = await record.Controller.UpdateActivityAsync(conversationId, _activity.Id) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            record.VerifyMocks();
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldNotCallHandlerWithoutActivity()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnUpdateActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);

            var result = await record.Controller.UpdateActivityAsync("", "");

            Assert.Null(result);
            record.VerifyMocks();
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldCallHandler()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnDeleteActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Once);

            await record.Controller.DeleteActivityAsync("", "");

            record.VerifyMocks();
        }

        [Fact]
        public async Task GetActivityMembersAsync_ShouldCallHandler()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnGetActivityMembersAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new()])
                .Verifiable(Times.Once);

            var result = await record.Controller.GetActivityMembersAsync("", "") as JsonResult;

            Assert.Single(result.Value as List<ChannelAccount>);
            record.VerifyMocks();
        }

        [Fact]
        public async Task CreateConversationAsync_ShouldCallHandler()
        {
            var record = UseRecord();
            var resource = new ConversationResourceResponse { Id = "test" };

            record.Handler.Setup(e => e.OnCreateConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<ConversationParameters>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(resource)
                .Verifiable(Times.Once);

            var result = await record.Controller.CreateConversationAsync(new()) as JsonResult;

            Assert.Equal(resource.Id, (result.Value as ConversationResourceResponse).Id);
            record.VerifyMocks();
        }

        [Fact]
        public async Task GetConversationsAsync_ShouldCallHandler()
        {
            var record = UseRecord();
            var conversationResult = new ConversationsResult { ContinuationToken = "token" };

            record.Handler.Setup(e => e.OnGetConversationsAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(conversationResult)
                .Verifiable(Times.Once);

            var result = await record.Controller.GetConversationsAsync(conversationResult.ContinuationToken) as JsonResult;

            Assert.Equal(conversationResult.ContinuationToken, (result.Value as ConversationsResult).ContinuationToken);
            record.VerifyMocks();
        }

        [Fact]
        public async Task GetConversationMembersAsync_ShouldCallHandler()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnGetConversationMembersAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new()])
                .Verifiable(Times.Once);

            var result = await record.Controller.GetConversationMembersAsync("") as JsonResult;

            Assert.Single(result.Value as List<ChannelAccount>);
            record.VerifyMocks();
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldCallHandler()
        {
            var record = UseRecord();
            var channelAccount = new ChannelAccount { Id = "test" };

            record.Handler.Setup(e => e.OnGetConversationMemberAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(channelAccount)
                .Verifiable(Times.Once);

            var result = await record.Controller.GetConversationMemberAsync("", "") as JsonResult;

            Assert.Equal(channelAccount.Id, (result.Value as ChannelAccount).Id);
            record.VerifyMocks();
        }

        [Fact]
        public async Task GetConversationPagedMembersAsync_ShouldCallHandler()
        {
            var record = UseRecord();
            var pagedMembersResult = new PagedMembersResult { ContinuationToken = "token" };

            record.Handler.Setup(e => e.OnGetConversationPagedMembersAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedMembersResult)
                .Verifiable(Times.Once);

            var result = await record.Controller.GetConversationPagedMembersAsync("") as JsonResult;

            Assert.Equal(pagedMembersResult.ContinuationToken, (result.Value as PagedMembersResult).ContinuationToken);
            record.VerifyMocks();
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldCallHandler()
        {
            var record = UseRecord();

            record.Handler.Setup(e => e.OnDeleteConversationMemberAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Once);

            await record.Controller.DeleteConversationMemberAsync("", "");

            record.VerifyMocks();
        }

        [Fact]
        public async Task SendConversationHistoryAsync_ShouldCallHandler()
        {
            var record = UseRecord();
            var conversationId = Guid.NewGuid().ToString();

            record.Handler.Setup(e => e.OnSendConversationHistoryAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<Transcript>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);

            var result = await record.Controller.SendConversationHistoryAsync(conversationId, null) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            record.VerifyMocks();
        }

        [Fact]
        public async Task UploadAttachmentAsync_ShouldCallHandler()
        {
            var record = UseRecord();
            var conversationId = Guid.NewGuid().ToString();

            record.Handler.Setup(e => e.OnUploadAttachmentAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<AttachmentData>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);

            var result = await record.Controller.UploadAttachmentAsync(conversationId, null) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            record.VerifyMocks();
        }

        private static Record UseRecord(Activity activity = null)
        {
            var handler = new Mock<IChannelApiHandler>();

            var content = activity == null ? "" : JsonSerializer.Serialize(activity);
            var controller = new ChannelApiController(handler.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(),
                        Request = { Body = new MemoryStream(Encoding.UTF8.GetBytes(content)) }
                    }
                }
            };
            return new(controller, handler);
        }

        private record Record(ChannelApiController Controller, Mock<IChannelApiHandler> Handler)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Handler);
            }
        }
    }
}