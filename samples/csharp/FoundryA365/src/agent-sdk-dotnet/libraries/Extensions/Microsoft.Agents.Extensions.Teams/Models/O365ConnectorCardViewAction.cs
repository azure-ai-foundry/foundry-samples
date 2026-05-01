// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// O365 connector card ViewAction action.
    /// </summary>
    public class O365ConnectorCardViewAction : O365ConnectorCardActionBase
    {
        /// <summary>
        /// Content type to be used in the @type property.
        /// </summary>
        public new const string Type = "ViewAction";

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardViewAction"/> class.
        /// </summary>
        public O365ConnectorCardViewAction()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardViewAction"/> class.
        /// </summary>
        /// <param name="type">Type of the action. Possible values include:
        /// 'ViewAction', 'OpenUri', 'HttpPOST', 'ActionCard'.</param>
        /// <param name="name">Name of the action that will be used as button title.</param>
        /// <param name="id">Action Id.</param>
        /// <param name="target">Target urls, only the first url effective for card button.</param>
        public O365ConnectorCardViewAction(string type = default, string name = default, string id = default, IList<string> target = default)
            : base(type, name, id)
        {
            Target = target;
        }

        /// <summary>
        /// Gets or sets target urls, only the first url effective for card button.
        /// </summary>
        /// <value>The target URLs.</value>
        public IList<string> Target { get; set; }
    }
}
