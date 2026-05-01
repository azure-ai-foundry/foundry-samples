// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.Dialogs.Choices
{
    /// <summary>
    /// A value that can be sorted and still refer to its original position with a source array.
    /// </summary>
    public class SortedValue
    {
        /// <summary>
        /// Gets or sets the value that will be sorted.
        /// </summary>
        /// <value>
        /// The value that will be sorted.
        /// </value>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the values original position within its unsorted array.
        /// </summary>
        /// <value>
        /// The values original position within its unsorted array.
        /// </value>
        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
}
