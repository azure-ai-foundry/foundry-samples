// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeConnectorRequest"/> that traces a get-attachment connector request,
    /// recording the attachment ID and view ID as span tags.
    /// </summary>
    internal class ScopeGetAttachment : ScopeConnectorRequest
    {
        private readonly string _attachmentId;
        private readonly string _viewId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Connector.Telemetry.Scopes.ScopeGetAttachment"/> class.
        /// </summary>
        /// <param name="attachmentId">The ID of the attachment to retrieve.</param>
        /// <param name="viewId">The view ID specifying the format of the attachment content.</param>
        public ScopeGetAttachment(string attachmentId, string viewId) : base(Constants.ScopeGetAttachment)
        {
            _attachmentId = attachmentId;
            _viewId = viewId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            base.Callback(activity, duration, exception);
            activity.SetTag(TagNames.AttachmentId, _attachmentId);
            activity.SetTag(TagNames.ViewId, _viewId);
        }
    }
}
