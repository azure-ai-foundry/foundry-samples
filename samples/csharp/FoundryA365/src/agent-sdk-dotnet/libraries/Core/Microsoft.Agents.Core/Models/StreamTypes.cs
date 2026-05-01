// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// The type of streaming message being sent.
    /// </summary>
    public static class StreamTypes
    {
        /// <summary>
        /// An informative update.
        /// </summary>
        /// <remarks>
        /// Informative messages
        ///    Can be sent in any order
        ///    Text is in Activity.Text
        ///    TextFormat can be supplied
        ///    Increments the streamSequence
        /// </remarks>
        public const string Informative = "informative";

        /// <summary>
        /// A chunk of partial message text.
        /// </summary>
        public const string Streaming = "streaming";

        /// <summary>
        /// The final message.  Only on final "message" Activity.
        /// </summary>
        public const string Final = "final";
    }
}
