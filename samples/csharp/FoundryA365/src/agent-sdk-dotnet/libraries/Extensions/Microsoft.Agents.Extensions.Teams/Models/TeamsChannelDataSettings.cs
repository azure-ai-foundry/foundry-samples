// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Settings within teams channel data specific to messages received in Microsoft Teams.
    /// </summary>
    public class TeamsChannelDataSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelDataSettings"/> class.
        /// </summary>
        public TeamsChannelDataSettings()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelDataSettings"/> class.
        /// </summary>
        /// <param name="channel">Information about the channel in which the message was sent.</param>
        public TeamsChannelDataSettings(ChannelInfo channel = default)
        {
            SelectedChannel = channel;
        }

        /// <summary>
        /// Gets or sets information about the selected Teams channel.
        /// </summary>
        /// <value>The selected Teams channel.</value>
        public ChannelInfo SelectedChannel { get; set; }

        /// <summary>
        /// Gets or sets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelDataSettings"/> type but that
        /// might appear in the REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
