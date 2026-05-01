// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces an upload-attachment connector request,
    /// recording the conversation ID as a span tag.
    /// </summary>
    internal class ScopeUploadAttachment : ScopeConnectorRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeUploadAttachment"/> class.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation the attachment is being uploaded to.</param>
        public ScopeUploadAttachment(string conversationId) : base(Constants.ScopeUploadAttachment, conversationId)
        { }
    }
}
