// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines values for EndOfConversationCodes.
    /// </summary>
    public static class EndOfConversationCodes
    {
        /// <summary>
        /// The code value for unknown end of conversations.
        /// </summary>
        public const string Unknown = "unknown";

        /// <summary>
        /// The code value for successful end of conversations.
        /// </summary>
        public const string CompletedSuccessfully = "completedSuccessfully";

        /// <summary>
        /// The code value for user cancelled end of conversations.
        /// </summary>
        public const string UserCancelled = "userCancelled";

        /// <summary>
        /// The code value for Agent time out end of conversations.
        /// </summary>
        public const string AgentTimedOut = "timedOut";

        /// <summary>
        /// The code value for Agent-issued invalid message end of conversations.
        /// </summary>
        public const string AgentIssuedInvalidMessage = "agentIssuedInvalidMessage";

        /// <summary>
        /// The code value for a general error.
        /// </summary>
        public const string Error = "error";
    }
}
