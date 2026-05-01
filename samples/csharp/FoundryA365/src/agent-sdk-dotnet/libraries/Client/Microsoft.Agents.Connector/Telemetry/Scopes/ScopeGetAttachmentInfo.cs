// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a get-attachment-info connector request,
    /// recording the attachment ID as a span tag.
    /// </summary>
    internal class ScopeGetAttachmentInfo : ScopeConnectorRequest
    {
        private readonly string _attachmentId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeGetAttachmentInfo"/> class.
        /// </summary>
        /// <param name="attachmentId">The ID of the attachment whose metadata is being retrieved.</param>
        public ScopeGetAttachmentInfo(string attachmentId) : base(Constants.ScopeGetAttachmentInfo)
        {
            _attachmentId = attachmentId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            base.Callback(activity, duration, exception);
            activity.SetTag(TagNames.AttachmentId, _attachmentId);
        }
    }
}
