// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Channel data specific to messages received in Microsoft Teams.
    /// </summary>
    public class TeamsChannelData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelData"/> class.
        /// </summary>
        public TeamsChannelData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelData"/> class.
        /// </summary>
        /// <param name="channel">Information about the channel in which the message was sent.</param>
        /// <param name="eventType">Type of event.</param>
        /// <param name="team">Information about the team in which the message was sent.</param>
        /// <param name="notification">Notification settings for the message.</param>
        /// <param name="tenant">Information about the tenant in which the
        /// message was sent.</param>
        public TeamsChannelData(ChannelInfo channel = default, string eventType = default, TeamInfo team = default, NotificationInfo notification = default, TenantInfo tenant = default)
            : this(channel, eventType, team, notification, tenant, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelData"/> class.
        /// </summary>
        /// <param name="channel">Information about the channel in which the message was sent.</param>
        /// <param name="eventType">Type of event.</param>
        /// <param name="team">Information about the team in which the message was sent.</param>
        /// <param name="notification">Notification settings for the message.</param>
        /// <param name="tenant">Information about the tenant in which the
        /// message was sent.</param>
        /// <param name="onBehalfOf">The OnBehalfOf information of the message.</param>
        public TeamsChannelData(ChannelInfo channel = default, string eventType = default, TeamInfo team = default, NotificationInfo notification = default, TenantInfo tenant = default, IList<OnBehalfOf> onBehalfOf = default)
        {
            Channel = channel;
            EventType = eventType;
            Team = team;
            Notification = notification;
            Tenant = tenant;
            OnBehalfOf = onBehalfOf ?? new List<OnBehalfOf>();
        }

        /// <summary>
        /// Gets or sets information about the channel in which the message was
        /// sent.
        /// </summary>
        /// <value>The channel information.</value>
        public ChannelInfo Channel { get; set; }

        /// <summary>
        /// Gets or sets type of event.
        /// </summary>
        /// <value>The type of event.</value>
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets information about the team in which the message was
        /// sent.
        /// </summary>
        /// <value>The information about the team.</value>
        public TeamInfo Team { get; set; }

        /// <summary>
        /// Gets or sets notification settings for the message.
        /// </summary>
        /// <value>The notification settings for the user.</value>
        public NotificationInfo Notification { get; set; }

        /// <summary>
        /// Gets or sets information about the tenant in which the message was
        /// sent.
        /// </summary>
        /// <value>The information about the tenant.</value>
        public TenantInfo Tenant { get; set; }

        /// <summary>
        /// Gets or sets information about the meeting in which the message was
        /// sent.
        /// </summary>
        /// <value>The information about the meeting.</value>
        public TeamsMeetingInfo Meeting { get; set; }

        /// <summary>
        /// Gets or sets information about the settings sent with this <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelData"/>.
        /// </summary>
        /// <value>The <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelDataSettings"/> for this <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelData"/>.</value>
        public TeamsChannelDataSettings Settings { get; set; }

        /// <summary>
        /// Gets the OnBehalfOf list for user attribution.
        /// </summary>
        /// <value>The Teams activity OnBehalfOf list.</value>
        public IList<OnBehalfOf> OnBehalfOf { get; set; }

        /// <summary>
        /// List of teams that the channel is shared with.
        /// </summary>
        public IList<TeamInfo>? SharedWithTeams { get; set; }

        /// <summary>
        /// List of teams that the channel was unshared from.
        /// </summary>
        public IList<TeamInfo>? UnsharedFromTeams { get; set; }

        /// <summary>
        /// Information about the source of a member that was added or removed from a shared channel.
        /// </summary>
        public MembershipSource? MembershipSource { get; set; }

        /// <summary>
        /// Gets or sets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelData"/> type but that
        /// might appear in the REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
