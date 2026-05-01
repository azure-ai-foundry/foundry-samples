// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.SharePoint.Models.Actions;

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint GetQuickView response object.
    /// </summary>
    public class QuickViewResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.QuickViewResponse"/> class.
        /// </summary>
        public QuickViewResponse()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets data for the quick view.
        /// </summary>
        /// <value>This value is the data of the quick view response.</value>
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets the template of quick view response.
        /// </summary>
        /// <value>This value is the template of the quick view response.</value>
        public object Template { get; set; }  // !!! was of type AdaptiveCard

        /// <summary>
        /// Gets or Sets view Id of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the view Id of the quick view response.</value>
        public string ViewId { get; set; }

        /// <summary>
        /// Gets or Sets title of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the title of the quick view response.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or Sets the external link parameters of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.ExternalLinkActionParameters"/>.
        /// </summary>
        /// <value>This value is the external link parameters of the quick view response.</value>
        public ExternalLinkActionParameters ExternalLink { get; set; }

        /// <summary>
        /// Gets or Sets focus parameters of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.FocusParameters"/>.
        /// </summary>
        /// <value>This value is the focus parameters of the quick view response.</value>
        public FocusParameters FocusParameters { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether the client to trigger a single sign on flow.
        /// </summary>
        /// <value>
        /// True if single sign on flow should be started.
        /// </value>
        public bool RequiresSso { get; set; }

        /// <summary>
        /// Gets or Sets a value which tells the client what view to load after SSO is complete.
        /// </summary>
        /// <value>
        /// ViewId to load from client after the SSO flow completes.
        /// </value>
        public string PostSsoViewId { get; set; }
    }
}
