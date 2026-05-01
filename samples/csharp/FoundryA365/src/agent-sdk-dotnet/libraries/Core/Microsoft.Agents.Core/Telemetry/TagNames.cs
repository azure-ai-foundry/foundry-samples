// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Telemetry
{
    /// <summary>
    /// Defines well-known tag (attribute) names used by the Microsoft Agents SDK when
    /// recording OpenTelemetry spans and metrics.
    /// </summary>
    /// <remarks>
    /// Using a consistent set of tag names across the SDK ensures that telemetry data
    /// is uniform and can be filtered, grouped, and queried predictably in any
    /// observability backend.
    /// </remarks>
    public static class TagNames
    {
        /// <summary>The delivery mode of the activity (e.g., "normal", "expectReplies").</summary>
        public static readonly string ActivityDeliveryMode = "activity.delivery_mode";

        /// <summary>The channel identifier on which the activity was sent or received.</summary>
        public static readonly string ActivityChannelId = "activity.channel_id";

        /// <summary>The unique identifier of the activity.</summary>
        public static readonly string ActivityId = "activity.id";

        /// <summary>The number of activities in a batch operation.</summary>
        public static readonly string ActivityCount = "activities.count";

        /// <summary>The type of the activity (e.g., "message", "event").</summary>
        public static readonly string ActivityType = "activity.type";

        /// <summary>The user identifier in an agentic scenario.</summary>
        public static readonly string AgenticUserId = "agentic.user_id";

        /// <summary>The instance identifier of the agentic application.</summary>
        public static readonly string AgenticInstanceId = "agentic.instance_id";

        /// <summary>The application (bot/agent) identifier.</summary>
        public static readonly string AppId = "agent.app_id";

        /// <summary>The identifier of a single attachment on an activity.</summary>
        public static readonly string AttachmentId = "activity.attachment.id";

        /// <summary>The number of attachments on an activity.</summary>
        public static readonly string AttachmentCount = "activity.attachments.count";

        /// <summary>The identifier of the authentication handler that processed the request.</summary>
        public static readonly string AuthHandlerId = "auth.handler.id";

        /// <summary>The authentication method used (e.g., "token", "certificate").</summary>
        public static readonly string AuthMethod = "auth.method";

        /// <summary>The OAuth/OIDC scopes requested or granted.</summary>
        public static readonly string AuthScopes = "auth.scopes";

        /// <summary>Whether the authentication attempt succeeded.</summary>
        public static readonly string AuthSuccess = "auth.success";

        /// <summary>The conversation identifier associated with the activity.</summary>
        public static readonly string ConversationId = "activity.conversation.id";

        /// <summary>The OAuth connection name used for user token operations.</summary>
        public static readonly string ExchangeConnection = "auth.connection.name";

        /// <summary>The HTTP method of an outgoing or incoming request.</summary>
        public static readonly string HttpMethod = "http.method";
        
        /// <summary>The HTTP status code of an outgoing or incoming response.</summary>
        public static readonly string HttpStatusCode = "http.status_code";

        /// <summary>Whether the current request is an agentic (agent-to-agent) request.</summary>
        public static readonly string IsAgentic = "activity.is_agentic_request";

        /// <summary>The number of keys involved in a storage operation.</summary>
        public static readonly string KeyCount = "storage.keys.count";

        /// <summary>A general-purpose operation name tag.</summary>
        public static readonly string Operation = "operation";

        /// <summary>Whether the incoming route was authorized.</summary>
        public static readonly string RouteAuthorized = "route.authorized";

        /// <summary>Whether the matched route is an invoke activity.</summary>
        public static readonly string RouteIsInvoke = "route.is_invoke";

        /// <summary>Whether the matched route is an agentic route.</summary>
        public static readonly string RouteIsAgentic = "route.is_agentic";

        /// <summary>Whether a route was matched for the incoming request.</summary>
        public static readonly string RouteMatched = "route.matched";

        /// <summary>The channel service URL the adapter connected to.</summary>
        public static readonly string ServiceUrl = "service_url";

        /// <summary>The type of storage operation (e.g., "read", "write", "delete").</summary>
        public static readonly string StorageOperation = "storage.operation";

        /// <summary>The endpoint of the token service used for user authentication.</summary>
        public static readonly string TokenServiceEndpoint = "agents.token_service.endpoint";

        /// <summary>The identifier of the user involved in the operation.</summary>
        public static readonly string UserId = "user.id";

        /// <summary>The identifier of a view (e.g., an Adaptive Card view).</summary>
        public static readonly string ViewId = "view.id";
    }
}
