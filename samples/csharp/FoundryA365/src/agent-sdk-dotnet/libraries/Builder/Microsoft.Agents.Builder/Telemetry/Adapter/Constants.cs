// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry.Adapter
{
    /// <summary>
    /// Defines the span (activity) and metric names used by the channel-adapter telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Span name for processing an incoming activity.</summary>
        internal static readonly string ScopeProcess = "agents.adapter.process";

        internal static readonly string ScopeWriteResponse = "agents.adapter.write_response";

        /// <summary>Span name for sending one or more outgoing activities.</summary>
        internal static readonly string ScopeSendActivities = "agents.adapter.send_activities";

        /// <summary>Span name for updating an existing activity.</summary>
        internal static readonly string ScopeUpdateActivity = "agents.adapter.update_activity";

        /// <summary>Span name for deleting an activity.</summary>
        internal static readonly string ScopeDeleteActivity = "agents.adapter.delete_activity";

        /// <summary>Span name for continuing (proactively resuming) a conversation.</summary>
        internal static readonly string ScopeContinueConversation = "agents.adapter.continue_conversation";

        /// <summary>Span name for creating a connector client.</summary>
        internal static readonly string ScopeCreateConnectorClient = "agents.adapter.create_connector_client";

        /// <summary>Span name for creating a user-token client.</summary>
        internal static readonly string ScopeCreateUserTokenClient = "agents.adapter.create_user_token_client";

        /// <summary>Metric name for the histogram that records activity-processing duration in milliseconds.</summary>
        internal static readonly string MetricAdapterProcessDuration = "agents.adapter.process.duration";

        /// <summary>Metric name for the counter of activities received by the adapter.</summary>
        internal static readonly string MetricActivitiesReceived = "agents.activities.received";

        /// <summary>Metric name for the counter of activities sent by the adapter.</summary>
        internal static readonly string MetricActivitiesSent = "agents.activities.sent";

        /// <summary>Metric name for the counter of activities updated by the adapter.</summary>
        internal static readonly string MetricActivitiesUpdated = "agents.activities.updated";

        /// <summary>Metric name for the counter of activities deleted by the adapter.</summary>
        internal static readonly string MetricActivitiesDeleted = "agents.activities.deleted";
    }
}
