// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry.TurnContext
{
    /// <summary>
    /// Defines the <see cref="System.Diagnostics.Activity"/> names used by the turn-context telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for sending activities from within a turn context.</summary>
        internal static readonly string ScopeSendActivities = "agents.turn.send_activities";
    }
}