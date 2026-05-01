// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Defines per-channel timing parameters for the typing worker.
    /// </summary>
    public interface ITypingChannelStrategy
    {
        /// <summary>
        /// Delay in milliseconds before the first typing activity is sent.
        /// </summary>
        int InitialDelayMs { get; }

        /// <summary>
        /// Interval in milliseconds between subsequent typing activities.
        /// </summary>
        int IntervalMs { get; }

        /// <summary>
        /// Gets a factory function that creates a typing activity for the specified turn context.
        /// </summary>
        /// <remarks>This factory can be used to generate typing indicators in a conversation, allowing
        /// for a more interactive user experience. The created activity can be sent to the user to indicate that the
        /// agent is 'typing'.</remarks>
        Func<ITurnContext, ConversationReference, IActivity> TypingFactory { get; }
    }
}
