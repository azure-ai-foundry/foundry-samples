// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// O365 connector card HttpPOST invoke query.
    /// </summary>
    public class O365ConnectorCardActionQuery
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardActionQuery"/> class.
        /// </summary>
        public O365ConnectorCardActionQuery()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardActionQuery"/> class.
        /// </summary>
        /// <param name="body">The results of body string defined in
        /// IO365ConnectorCardHttpPOST with substituted input values.</param>
        /// <param name="actionId">Action Id associated with the HttpPOST
        /// action button triggered, defined in
        /// O365ConnectorCardActionBase.</param>
        public O365ConnectorCardActionQuery(string body = default, string actionId = default)
        {
            Body = body;
            ActionId = actionId;
        }

        /// <summary>
        /// Gets or sets the results of body string defined in
        /// IO365ConnectorCardHttpPOST with substituted input values.
        /// </summary>
        /// <value>The body defined in IO365ConnectorCardHttpPost.</value>
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets action ID associated with the HttpPOST action button
        /// triggered, defined in O365ConnectorCardActionBase.
        /// </summary>
        /// <value>The action ID associated with the HttpPOST action button triggered.</value>
        public string ActionId { get; set; }
    }
}
