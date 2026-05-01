// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Define values for common event names used by activities of type <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Event"/>.
    /// </summary>
    public static class ActivityEventNames
    {
        /// <summary>
        /// The event name for continuing a conversation.
        /// </summary>
        public const string ContinueConversation = "ContinueConversation";

        /// <summary>
        /// The event name for creating a conversation.
        /// </summary>
        public const string CreateConversation = "CreateConversation";
    }
}
