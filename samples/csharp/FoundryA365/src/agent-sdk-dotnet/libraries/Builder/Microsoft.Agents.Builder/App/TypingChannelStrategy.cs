// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Concrete per-channel typing strategy with configurable initial delay and interval.
    /// </summary>
    public sealed class TypingChannelStrategy(int initialDelayMs, int intervalMs, Func<ITurnContext, ConversationReference, IActivity> typingFactory = null) : ITypingChannelStrategy
    {
        /// <inheritdoc/>
        public int InitialDelayMs => initialDelayMs;

        /// <inheritdoc/>
        public int IntervalMs => intervalMs;

        /// <inheritdoc/>
        public Func<ITurnContext, ConversationReference, IActivity> TypingFactory => typingFactory ?? ((context, reference) => new Activity(type: ActivityTypes.Typing, relatesTo: reference));
    }
}
