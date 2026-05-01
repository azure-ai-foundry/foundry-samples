// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines values for CardAction Types.
    /// </summary>
    public static class ActionTypes
    {
        /// <summary>
        /// An openUrl action represents a hyperlink to be handled by the client. Open URL uses the following fields:
        /// <code>
        ///    type("openUrl")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string OpenUrl = "openUrl";

        /// <summary>
        /// An imBack action represents a text response that is added to the chat feed. IM Back uses the following fields:
        /// <code>
        ///    type("imBack")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string ImBack = "imBack";

        /// <summary>
        /// A postBack action represents a text response that is not added to the chat feed. Post Back uses the following fields:
        /// <code>
        ///    type("postBack")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string PostBack = "postBack";

        /// <summary>
        /// A playAudio action represents audio media that may be played. Play Audio uses the following fields:
        /// <code>
        ///    type("playAudio")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string PlayAudio = "playAudio";

        /// <summary>
        /// A playVideo action represents video media that may be played. Play Video uses the following fields:
        /// <code>
        ///    type("playVideo")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string PlayVideo = "playVideo";

        /// <summary>
        /// An showImage action represents an image that may be displayed. ShowImage uses the following fields:
        /// <code>
        ///    type("showImage")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string ShowImage = "showImage";

        /// <summary>
        /// An downloadFile action represents a hyperlink to be downloaded. Download File uses the following fields:
        /// <code>
        ///    type("downloadFile")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string DownloadFile = "downloadFile";

        /// <summary>
        /// A signin action represents a hyperlink to be handled by the client's signin system. Signin uses the following fields:
        /// <code>
        ///    type("signin")
        ///    title
        ///    image
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string Signin = "signin";

        /// <summary>
        /// A call action represents a telephone number that may be called. Call uses the following fields:
        /// <code>
        ///    type("call")
        ///    title
        ///    image
        ///    value `tel://' + number
        /// </code>
        /// </summary>
        public const string Call = "call";

        /// <summary>
        /// A messageBack action represents a text response to be sent via the chat system. Message Back uses the following fields:
        /// <code>
        ///    type("messageBack")
        ///    title
        ///    image
        ///    text
        ///    displayText
        ///    value(of any type)
        /// </code>
        /// </summary>
        public const string MessageBack = "messageBack";

        /// <summary>
        /// An app name to open.
        /// </summary>
        public const string OpenApp = "openApp";
    }
}
