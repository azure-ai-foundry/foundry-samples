// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// The Connector API allows communicating back to the incoming request.  Typically to the Activity.ServiceUrl property.
    /// </summary>
    public interface IConnectorClient : System.IDisposable
    {
        /// <summary>
        /// Gets or sets the base URI of the service.
        /// </summary>
        /// <value>The base URI.</value>
        System.Uri BaseUri { get; }

        /// <summary>
        /// Gets the IAttachments.</summary>
        /// <value>See <see cref="Microsoft.Agents.Connector.IAttachments" /> class.</value>
        IAttachments Attachments { get; }

        /// <summary> Gets the IConversations.</summary>
        /// <value>See <see cref="Microsoft.Agents.Connector.IConversations" /> class.</value>
        IConversations Conversations { get; }
    }
}
