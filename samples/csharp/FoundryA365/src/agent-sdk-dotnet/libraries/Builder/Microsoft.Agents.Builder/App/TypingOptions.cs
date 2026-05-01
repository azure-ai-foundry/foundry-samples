// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Options for controlling the typing indicator behavior.
    /// </summary>
    public class TypingOptions
    {
        /// <summary>
        /// Delay in milliseconds before the first typing activity is sent. Defaults to 500ms.
        /// </summary>
        public int InitialDelayMs { get; set; } = 500;

        /// <summary>
        /// Interval in milliseconds between subsequent typing activities. Defaults to 2000ms.
        /// </summary>
        public int IntervalMs { get; set; } = 2000;

        /// <summary>
        /// Channel-specific timing overrides. Keys are channel IDs (case-insensitive).
        /// </summary>
        /// <remarks>
        /// M365Copilot requires the first typing activity within a short window, so its
        /// initial delay defaults to 250ms.
        /// </remarks>
        public IDictionary<string, ITypingChannelStrategy> ChannelStrategies { get; set; } =
            new Dictionary<string, ITypingChannelStrategy>(System.StringComparer.OrdinalIgnoreCase)
            {
                [Channels.M365Copilot] = new TypingChannelStrategy(initialDelayMs: 250, intervalMs: 1000)
            };
    }
}
