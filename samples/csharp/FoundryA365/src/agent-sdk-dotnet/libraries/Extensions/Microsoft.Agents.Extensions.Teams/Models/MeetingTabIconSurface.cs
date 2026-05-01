// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies meeting tab icon surface.
    /// </summary>
    public class MeetingTabIconSurface : Surface
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingTabIconSurface"/> class.
        /// </summary>
        public MeetingTabIconSurface()
            : base(SurfaceType.MeetingTabIcon)
        {
        }

        /// <summary>
        /// Gets or sets optional field tab entity Id of this <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingTabIconSurface"/>.
        /// </summary>
        /// <value>
        /// The tab entity Id of this <see cref="Microsoft.Agents.Extensions.Teams.Models.MeetingTabIconSurface"/>.
        /// </value>
        public string TabEntityId { get; set; }
    }
}
