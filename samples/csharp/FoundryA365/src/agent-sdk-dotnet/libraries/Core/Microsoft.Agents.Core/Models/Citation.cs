// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Citations used in the message.
    /// </summary>
    /// <remarks>
    /// Constructs a citation.
    /// </remarks>
    /// <param name="content">The content of the citation.</param>
    /// <param name="title">The title of the citation.</param>
    /// <param name="url">The url of the citation.</param>
    public class Citation(string content, string title, string url)
    {
        /// <summary>
        /// The content of the citation.
        /// </summary>
        public string Content { get; set; } = content;

        /// <summary>
        /// The title of the citation.
        /// </summary>
        public string Title { get; set; } = title;

        /// <summary>
        /// The URL of the citation.
        /// </summary>
        public string Url { get; set; } = url;
    }
}
