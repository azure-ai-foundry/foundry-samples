// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Core.Telemetry
{
    /// <summary>
    /// Provides helper methods for formatting values used in telemetry tags.
    /// </summary>
    public static class TelemetryUtils
    {
        /// <summary>
        /// A sentinel value used when a telemetry tag value is unavailable or cannot be determined.
        /// </summary>
        public static readonly string Unknown = "unknown";

        /// <summary>
        /// Formats a collection of OAuth/OIDC scopes into a comma-separated string suitable
        /// for use as a telemetry tag value.
        /// </summary>
        /// <param name="scopes">The scopes to format, or <c>null</c>.</param>
        /// <returns>
        /// A comma-separated string of the provided scopes, or <see cref="Microsoft.Agents.Core.Telemetry.TelemetryUtils.Unknown"/> if
        /// <paramref name="scopes"/> is <c>null</c> or empty.
        /// </returns>
        public static string FormatScopes(IEnumerable<string>? scopes)
        {
            if (scopes == null || !scopes.Any())
            {
                return Unknown;
            }
            return string.Join(",", scopes);
        }
    }
}