// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the execution of a matched route handler
    /// during a turn.
    /// </summary>
    /// <remarks>
    /// Tags the <see cref="System.Diagnostics.Activity"/> with whether the route is an invoke
    /// activity and whether it is an agentic route.
    /// </remarks>
    internal class ScopeRouteHandler : TelemetryScope
    {
        private readonly bool _isInvoke;
        private readonly bool _isAgentic;

        public ScopeRouteHandler(bool isInvoke, bool isAgentic) : base(Constants.ScopeRouteHandler)
        {
            _isInvoke = isInvoke;
            _isAgentic = isAgentic;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.RouteIsInvoke, _isInvoke);
            activity.SetTag(TagNames.RouteIsAgentic, _isAgentic);
        }
    }
}