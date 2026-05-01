// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Refers to a substring of content within another field. </summary>
    public class TextHighlight
    {
        public TextHighlight() { }

        /// <summary> Initializes a new instance of TextHighlight. </summary>
        /// <param name="text"> Defines the snippet of text to highlight. </param>
        /// <param name="occurrence"> Occurrence of the text field within the referenced text, if multiple exist. </param>
        public TextHighlight(string text = default, int? occurrence = default)
        {
            Text = text;
            Occurrence = occurrence;
        }

        /// <summary> Defines the snippet of text to highlight. </summary>
        public string Text { get; set; }
        /// <summary> Occurrence of the text field within the referenced text, if multiple exist. </summary>
        public int? Occurrence { get; set; }
    }
}
