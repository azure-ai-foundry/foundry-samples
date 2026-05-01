// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint GetPropertyPaneConfiguration response object.
    /// </summary>
    public class GetPropertyPaneConfigurationResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.GetPropertyPaneConfigurationResponse"/> class.
        /// </summary>
        public GetPropertyPaneConfigurationResponse()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the pages of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPanePage"/>.
        /// </summary>
        /// <value>This value is the pages of the property pane.</value>
        public IEnumerable<PropertyPanePage> Pages { get; set; }

        /// <summary>
        /// Gets or Sets the current page of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the current page of the property pane.</value>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Gets or Sets the loading indicator delay time of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the loading indicator delay time of the property pane.</value>
        public int LoadingIndicatorDelayTime { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the loading indicator should be displayed on top of the property pane or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value sets whether the loading indicator is shown for the property pane.</value>
        public bool ShowLoadingIndicator { get; set; }
    }
}
