// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Core.Telemetry.TelemetryScope"/> that traces a user-token REST client request and records
    /// connection name, user ID, and channel ID as span tags together with request count and duration metrics.
    /// </summary>
    internal class ScopeUserTokenRestClientRequest : TelemetryScope
    {
        private readonly string? _connectionName;
        private readonly string? _userId;
        private readonly string? _channelId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUserTokenRestClientRequest"/> class.
        /// </summary>
        /// <param name="spanName">The name of the telemetry span.</param>
        /// <param name="connectionName">The OAuth connection name, or <see langword="null"/>.</param>
        /// <param name="userId">The user ID, or <see langword="null"/>.</param>
        /// <param name="channelId">The channel ID, or <see langword="null"/>.</param>
        public ScopeUserTokenRestClientRequest(string spanName, string? connectionName = null, string? userId = null, string? channelId = null)
            : base(spanName)
        {
            _connectionName = connectionName;
            _userId = userId;
            _channelId = channelId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            if (_connectionName != null)
            {
                activity.SetTag(TagNames.ExchangeConnection, _connectionName);
            }
            if (_userId != null)
            {
                activity.SetTag(TagNames.UserId, _userId);
            }
            if (_channelId != null)
            {
                activity.SetTag(TagNames.ActivityChannelId, _channelId);
            }
            Metrics.UserTokenRestClientRequestCount.Add(1);
            Metrics.UserTokenRestClientRequestDuration.Record(duration);
        }
    }
}
