// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Messaging extension query parameters.
    /// </summary>
    public class MessagingExtensionParameter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionParameter"/> class.
        /// </summary>
        public MessagingExtensionParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessagingExtensionParameter"/> class.
        /// </summary>
        /// <param name="name">Name of the parameter.</param>
        /// <param name="value">Value of the parameter.</param>
        public MessagingExtensionParameter(string name = default, object value = default)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Gets or sets name of the parameter.
        /// </summary>
        /// <value>The name of the parameter.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets value of the parameter.
        /// </summary>
        /// <value>The value of the parameter.</value>
        public object Value { get; set; }
    }
}
