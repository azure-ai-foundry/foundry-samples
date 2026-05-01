// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Agents.Connector.Telemetry.Scopes;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.TestSupport;
using Xunit;

namespace Microsoft.Agents.Connector.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class ConnectorScopeTests : TelemetryScopeTestBase
    {
        #region ScopeReplyToActivity

        [Fact]
        public void ScopeReplyToActivity_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeReplyToActivity("conv-1", "act-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.reply_to_activity", started.OperationName);
        }

        [Fact]
        public void ScopeReplyToActivity_Callback_SetsConversationAndActivityIdTags()
        {
            var scope = new ScopeReplyToActivity("conv-123", "act-456");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("act-456", stopped.GetTagItem(TagNames.ActivityId));
        }

        [Fact]
        public void ScopeReplyToActivity_SetError_SetsErrorStatus()
        {
            var scope = new ScopeReplyToActivity("conv-1", "act-1");
            scope.SetError(new System.InvalidOperationException("reply failed"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("reply failed", stopped.StatusDescription);
        }

        #endregion

        #region ScopeSendToConversation

        [Fact]
        public void ScopeSendToConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeSendToConversation("conv-1", null);

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.send_to_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeSendToConversation_Callback_SetsConversationIdTag()
        {
            var scope = new ScopeSendToConversation("conv-123", null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void ScopeSendToConversation_Callback_SetsActivityIdTag_WhenProvided()
        {
            var scope = new ScopeSendToConversation("conv-123", "act-456");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("act-456", stopped.GetTagItem(TagNames.ActivityId));
        }

        #endregion

        #region ScopeUpdateActivity

        [Fact]
        public void ScopeUpdateActivity_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeUpdateActivity("conv-1", "act-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.update_activity", started.OperationName);
        }

        [Fact]
        public void ScopeUpdateActivity_Callback_SetsConversationAndActivityIdTags()
        {
            var scope = new ScopeUpdateActivity("conv-123", "act-456");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("act-456", stopped.GetTagItem(TagNames.ActivityId));
        }

        #endregion

        #region ScopeDeleteActivity

        [Fact]
        public void ScopeDeleteActivity_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeDeleteActivity("conv-1", "act-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.delete_activity", started.OperationName);
        }

        [Fact]
        public void ScopeDeleteActivity_Callback_SetsConversationAndActivityIdTags()
        {
            var scope = new ScopeDeleteActivity("conv-123", "act-456");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("act-456", stopped.GetTagItem(TagNames.ActivityId));
        }

        #endregion

        #region ScopeCreateConversation

        [Fact]
        public void ScopeCreateConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeCreateConversation();

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.create_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeCreateConversation_Callback_DoesNotThrow_WithNoTags()
        {
            var scope = new ScopeCreateConversation();
            scope.Dispose();

            Assert.Single(StoppedActivities);
        }

        #endregion

        #region ScopeGetConversations

        [Fact]
        public void ScopeGetConversations_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetConversations();

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.get_conversations", started.OperationName);
        }

        #endregion

        #region ScopeGetConversationMembers

        [Fact]
        public void ScopeGetConversationMembers_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetConversationMembers("conv-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.get_conversation_members", started.OperationName);
        }

        [Fact]
        public void ScopeGetConversationMembers_Callback_SetsConversationIdTag()
        {
            var scope = new ScopeGetConversationMembers("conv-123");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
        }

        #endregion

        #region ScopeUploadAttachment

        [Fact]
        public void ScopeUploadAttachment_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeUploadAttachment("conv-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.upload_attachment", started.OperationName);
        }

        [Fact]
        public void ScopeUploadAttachment_Callback_SetsConversationIdTag()
        {
            var scope = new ScopeUploadAttachment("conv-123");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
        }

        #endregion

        #region ScopeGetAttachment

        [Fact]
        public void ScopeGetAttachment_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetAttachment("att-1", "view-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.get_attachment", started.OperationName);
        }

        [Fact]
        public void ScopeGetAttachment_Callback_SetsAttachmentIdAndViewIdTags()
        {
            var scope = new ScopeGetAttachment("att-123", "original");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("att-123", stopped.GetTagItem(TagNames.AttachmentId));
            Assert.Equal("original", stopped.GetTagItem(TagNames.ViewId));
        }

        #endregion

        #region ScopeGetAttachmentInfo

        [Fact]
        public void ScopeGetAttachmentInfo_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetAttachmentInfo("att-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.connector.get_attachment_info", started.OperationName);
        }

        [Fact]
        public void ScopeGetAttachmentInfo_Callback_SetsAttachmentIdTag()
        {
            var scope = new ScopeGetAttachmentInfo("att-123");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("att-123", stopped.GetTagItem(TagNames.AttachmentId));
        }

        #endregion

        #region ScopeGetToken

        [Fact]
        public void ScopeGetToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetToken("conn-1", "user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.get_user_token", started.OperationName);
        }

        [Fact]
        public void ScopeGetToken_Callback_SetsConnectionNameAndUserIdTags()
        {
            var scope = new ScopeGetToken("myConnection", "user-123");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("myConnection", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("user-123", stopped.GetTagItem(TagNames.UserId));
        }

        [Fact]
        public void ScopeGetToken_Callback_SetsChannelIdTag_WhenProvided()
        {
            var scope = new ScopeGetToken("myConnection", "user-123", "msteams");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("msteams", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        #endregion

        #region ScopeGetAadTokens

        [Fact]
        public void ScopeGetAadTokens_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetAadTokens("conn-1", "user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.get_aad_tokens", started.OperationName);
        }

        [Fact]
        public void ScopeGetAadTokens_Callback_SetsConnectionNameAndUserIdTags()
        {
            var scope = new ScopeGetAadTokens("aadConnection", "user-456");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("aadConnection", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("user-456", stopped.GetTagItem(TagNames.UserId));
        }

        #endregion

        #region ScopeExchangeToken

        [Fact]
        public void ScopeExchangeToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeExchangeToken("conn-1", "user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.exchange_token", started.OperationName);
        }

        [Fact]
        public void ScopeExchangeToken_Callback_SetsConnectionNameAndUserIdTags()
        {
            var scope = new ScopeExchangeToken("exchangeConn", "user-789");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("exchangeConn", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("user-789", stopped.GetTagItem(TagNames.UserId));
        }

        [Fact]
        public void ScopeExchangeToken_Callback_SetsChannelIdTag_WhenProvided()
        {
            var scope = new ScopeExchangeToken("exchangeConn", "user-789", "directline");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("directline", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        #endregion

        #region ScopeGetSignInResource

        [Fact]
        public void ScopeGetSignInResource_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetSignInResource("conn-1", "user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.get_sign_in_resource", started.OperationName);
        }

        [Fact]
        public void ScopeGetSignInResource_Callback_SetsConnectionNameAndUserIdTags()
        {
            var scope = new ScopeGetSignInResource("signInConn", "user-321");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("signInConn", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("user-321", stopped.GetTagItem(TagNames.UserId));
        }

        #endregion

        #region ScopeGetTokenOrSignInResource

        [Fact]
        public void ScopeGetTokenOrSignInResource_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetTokenOrSignInResource("conn-1", "user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.get_token_or_sign_in_resource", started.OperationName);
        }

        [Fact]
        public void ScopeGetTokenOrSignInResource_Callback_SetsConnectionNameAndUserIdTags()
        {
            var scope = new ScopeGetTokenOrSignInResource("tokenOrSignInConn", "user-654");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("tokenOrSignInConn", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("user-654", stopped.GetTagItem(TagNames.UserId));
        }

        #endregion

        #region ScopeGetTokenStatus

        [Fact]
        public void ScopeGetTokenStatus_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetTokenStatus("user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.get_token_status", started.OperationName);
        }

        [Fact]
        public void ScopeGetTokenStatus_Callback_SetsUserIdTag()
        {
            var scope = new ScopeGetTokenStatus("user-987");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("user-987", stopped.GetTagItem(TagNames.UserId));
        }

        [Fact]
        public void ScopeGetTokenStatus_Callback_SetsChannelIdTag_WhenProvided()
        {
            var scope = new ScopeGetTokenStatus("user-987", "webchat");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("webchat", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        #endregion

        #region ScopeSignOut

        [Fact]
        public void ScopeSignOut_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeSignOut("conn-1", "user-1");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.user_token_client.sign_out", started.OperationName);
        }

        [Fact]
        public void ScopeSignOut_Callback_SetsConnectionNameAndUserIdTags()
        {
            var scope = new ScopeSignOut("signOutConn", "user-111");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("signOutConn", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("user-111", stopped.GetTagItem(TagNames.UserId));
        }

        [Fact]
        public void ScopeSignOut_Callback_SetsChannelIdTag_WhenProvided()
        {
            var scope = new ScopeSignOut("signOutConn", "user-111", "emulator");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("emulator", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        #endregion
    }
}
