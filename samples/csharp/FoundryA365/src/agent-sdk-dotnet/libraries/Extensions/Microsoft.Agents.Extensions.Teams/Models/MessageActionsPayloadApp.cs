// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Represents an application entity.
    /// </summary>
    public class MessageActionsPayloadApp
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessageActionsPayloadApp"/> class.
        /// </summary>
        public MessageActionsPayloadApp()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessageActionsPayloadApp"/> class.
        /// </summary>
        /// <param name="applicationIdentityType">The type of application.
        /// Possible values include: 'aadApplication', 'bot', 'tenantBot',
        /// 'office365Connector', 'webhook'.</param>
        /// <param name="id">The id of the application.</param>
        /// <param name="displayName">The plaintext display name of the
        /// application.</param>
        public MessageActionsPayloadApp(string applicationIdentityType = default, string id = default, string displayName = default)
        {
            ApplicationIdentityType = applicationIdentityType;
            Id = id;
            DisplayName = displayName;
        }

        /// <summary>
        /// Gets or sets the type of application. Possible values include:
        /// 'aadApplication', 'bot', 'tenantBot', 'office365Connector',
        /// 'webhook'.
        /// </summary>
        /// <value>The type of application.</value>
        public string ApplicationIdentityType { get; set; }

        /// <summary>
        /// Gets or sets the id of the application.
        /// </summary>
        /// <value>The application ID.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the plaintext display name of the application.
        /// </summary>
        /// <value>The plaintext display name of the application.</value>
        public string DisplayName { get; set; }
    }
}
