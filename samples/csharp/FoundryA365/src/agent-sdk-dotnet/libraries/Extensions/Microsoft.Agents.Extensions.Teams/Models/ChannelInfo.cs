// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// A channel info object which describes the channel.
    /// </summary>
    public class ChannelInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ChannelInfo"/> class.
        /// </summary>
        public ChannelInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ChannelInfo"/> class.
        /// </summary>
        /// <param name="id">Unique identifier representing a channel.</param>
        /// <param name="name">Name of the channel.</param>
        public ChannelInfo(string id = default, string name = default)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Gets or sets unique identifier representing a channel.
        /// </summary>
        /// <value>The channel ID.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets name of the channel.
        /// </summary>
        /// <value>The channel name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets type of the channel. Valid values are standard, shared and private.
        /// </summary>
        /// <value>The channel type.</value>
        public string Type { get; set; }
    }
}
