// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Telemetry
{
    /// <summary>
    /// Telemetry constant names for spans and metrics used by the connector and user-token REST clients.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for replying to an existing activity.</summary>
        internal static readonly string ScopeReplyToActivity = "agents.connector.reply_to_activity";

        /// <summary>Activity name for sending an activity to a conversation.</summary>
        internal static readonly string ScopeSendToConversation = "agents.connector.send_to_conversation";

        /// <summary>Activity name for updating an existing activity via the connector.</summary>
        internal static readonly string ScopeUpdateActivity = "agents.connector.update_activity";

        /// <summary>Activity name for deleting an activity via the connector.</summary>
        internal static readonly string ScopeDeleteActivity = "agents.connector.delete_activity";

        /// <summary>Activity name for creating a new conversation.</summary>
        internal static readonly string ScopeCreateConversation = "agents.connector.create_conversation";

        /// <summary>Activity name for retrieving a list of conversations.</summary>
        internal static readonly string ScopeGetConversations = "agents.connector.get_conversations";

        /// <summary>Activity name for retrieving the members of a conversation.</summary>
        internal static readonly string ScopeGetConversationMembers = "agents.connector.get_conversation_members";

        /// <summary>Activity name for uploading an attachment to a conversation.</summary>
        internal static readonly string ScopeUploadAttachment = "agents.connector.upload_attachment";

        /// <summary>Activity name for downloading an attachment.</summary>
        internal static readonly string ScopeGetAttachment = "agents.connector.get_attachment";

        /// <summary>Activity name for retrieving attachment metadata.</summary>
        internal static readonly string ScopeGetAttachmentInfo = "agents.connector.get_attachment_info";

        /// <summary>Activity name for retrieving a user token via the token service.</summary>
        internal static readonly string ScopeGetToken = "agents.user_token_client.get_user_token";

        /// <summary>Activity name for signing a user out via the token service.</summary>
        internal static readonly string ScopeSignOut = "agents.user_token_client.sign_out";

        /// <summary>Activity name for retrieving a sign-in resource URL.</summary>
        internal static readonly string ScopeGetSignInResource = "agents.user_token_client.get_sign_in_resource";

        /// <summary>Activity name for exchanging a token via the token service.</summary>
        internal static readonly string ScopeExchangeToken = "agents.user_token_client.exchange_token";

        /// <summary>Activity name for retrieving a token or sign-in resource in a single call.</summary>
        internal static readonly string ScopeGetTokenOrSignInResource = "agents.user_token_client.get_token_or_sign_in_resource";

        /// <summary>Activity name for retrieving the token status for a user.</summary>
        internal static readonly string ScopeGetTokenStatus = "agents.user_token_client.get_token_status";

        /// <summary>Activity name for retrieving AAD tokens for a user.</summary>
        internal static readonly string ScopeGetAadTokens = "agents.user_token_client.get_aad_tokens";

        /// <summary>Metric name for the counter of connector REST client requests.</summary>
        internal static readonly string MetricConnectorRequestCount = "agents.connector.request.count";

        /// <summary>Metric name for the histogram that records connector REST client request duration in milliseconds.</summary>
        internal static readonly string MetricConnectorRequestDuration = "agents.connector.request.duration";

        /// <summary>Metric name for the counter of user-token REST client requests.</summary>
        internal static readonly string MetricUserTokenRestClientRequestCount = "agents.user_token_client.request.count";

        /// <summary>Metric name for the histogram that records user-token REST client request duration in milliseconds.</summary>
        internal static readonly string MetricUserTokenRestClientRequestDuration = "agents.user_token_client.request.duration";
    }
}
