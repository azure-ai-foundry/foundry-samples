// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Set of key-value pairs. Advantage of this section is that key and value properties will be
    /// rendered with default style information with some delimiter between them. So there is no need for developer to specify style information.
    /// </summary>
    public class Fact
    {
        /// <summary> Initializes a new instance of Fact. </summary>
        public Fact()
        {
        }

        /// <summary> Initializes a new instance of Fact. </summary>
        /// <param name="key"> The key for this Fact. </param>
        /// <param name="value"> The value for this Fact. </param>
        public Fact(string key = default, string value = default)
        {
            Key = key;
            Value = value;
        }

        /// <summary> The key for this Fact. </summary>
        public string Key { get; set; }
        /// <summary> The value for this Fact. </summary>
        public string Value { get; set; }
    }
}
