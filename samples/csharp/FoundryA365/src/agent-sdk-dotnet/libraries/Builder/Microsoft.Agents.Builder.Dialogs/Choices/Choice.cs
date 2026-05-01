// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Builder.Dialogs.Choices
{
    /// <summary>
    /// Represents a choice for a choice prompt.
    /// </summary>
    public class Choice
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Choice"/> class to add a choice to a choice prompt.
        /// </summary>
        /// <param name="value">The value to return when the choice is selected.</param>
        public Choice(string value = null)
        {
            Value = value;
        }

        /// <summary>
        /// Gets or sets the value to return when selected.
        /// </summary>
        /// <value>
        /// The value to return when selected.
        /// </value>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the action to use when rendering the choice as a suggested action or hero card.
        /// This is optional.
        /// </summary>
        /// <value>
        /// The action to use when rendering the choice as a suggested action or hero card.
        /// </value>
        [JsonPropertyName("action")]
        public CardAction Action { get; set; }

        /// <summary>
        /// Gets or sets the list of synonyms to recognize in addition to the value. This is optional.
        /// </summary>
        /// <value>
        /// The list of synonyms to recognize in addition to the value.
        /// </value>
        [JsonPropertyName("synonyms")]
        public List<string> Synonyms { get; set; }
    }
}
