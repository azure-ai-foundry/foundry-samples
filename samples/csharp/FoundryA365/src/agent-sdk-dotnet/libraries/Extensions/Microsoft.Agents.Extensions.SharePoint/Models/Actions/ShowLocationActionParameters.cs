// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint parameters for a show location action.
    /// </summary>
    public class ShowLocationActionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.ShowLocationActionParameters"/> class.
        /// </summary>
        public ShowLocationActionParameters()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the location coordinates of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.Location"/>.
        /// </summary>
        /// <value>This value is the location to be shown.</value>
        public Location LocationCoordinates { get; set; }
    }
}
