// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// DeliveryModes define how an Agent receiving an Activity replies to the sender.
    /// <see cref="Microsoft.Agents.Core.Models.IActivity.DeliveryMode"/>
    /// </summary>
    public static class DeliveryModes
    {
        /// <summary>
        /// When specified on an Activity being sent, the receiver sends replies
        /// via HTTP POSTs back the Activity.ServiceUrl.  These replies will happen
        /// asynchronously, including after the sending Turn is over.
        /// </summary>
        public const string Normal = "normal";

        /// <summary>
        /// When specified on an Activity being sent
        /// </summary>
        public const string ExpectReplies = "expectReplies";

        /// <summary>
        /// When specified on an Activity being sent, replies from the receiver
        /// are streamed back in the HTTP response.
        /// </summary>
        public const string Stream = "stream";
    }
}
