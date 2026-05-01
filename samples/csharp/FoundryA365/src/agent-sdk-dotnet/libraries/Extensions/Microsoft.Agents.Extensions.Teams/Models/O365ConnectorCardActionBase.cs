// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// O365 connector card action base.
    /// </summary>
    public class O365ConnectorCardActionBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardActionBase"/> class.
        /// </summary>
        public O365ConnectorCardActionBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardActionBase"/> class.
        /// </summary>
        /// <param name="type">Type of the action. Possible values include:
        /// 'ViewAction', 'OpenUri', 'HttpPOST', 'ActionCard'.</param>
        /// <param name="name">Name of the action that will be used as button
        /// title.</param>
        /// <param name="id">Action Id.</param>
        public O365ConnectorCardActionBase(string type = default, string name = default, string id = default)
        {
            Type = type;
            Name = name;
            Id = id;
        }

        /// <summary>
        /// Gets or sets type of the action. Possible values include:
        /// 'ViewAction', 'OpenUri', 'HttpPOST', 'ActionCard'.
        /// </summary>
        /// <value>The type of the action.</value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets name of the action that will be used as button title.
        /// </summary>
        /// <value>The name of the action that will be used as a button title.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets action ID.
        /// </summary>
        /// <value>The action ID.</value>
        public string Id { get; set; }
    }
}
