// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Stream information will be included in an Entity in the Activity.
    /// </summary>
    public class StreamInfo : Entity
    {
        public StreamInfo() : base(EntityTypes.StreamInfo)
        {
            StreamType = StreamTypes.Streaming;
        }

        /// <summary>
        /// Required unique ID.  This remains the same for all subsequent Entities part of the stream operation.
        /// </summary>
        public string StreamId { get; set; }

        public string StreamType { get; set; }

        /// <summary>
        /// Required incrementing integer for each Typing Activity sent, and final Message.
        /// </summary>
        public int? StreamSequence { get; set; }

        /// <summary>
        /// Optional stream result when StreamType is Final.
        /// </summary>
        public string StreamResult { get; set; }
    }
}
