// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the downloading of file attachments
    /// during a turn.
    /// </summary>
    /// <remarks>
    /// Tags the <see cref="System.Diagnostics.Activity"/> with the number of attachments
    /// present on the current activity.
    /// </remarks>
    internal class ScopeDownloadFiles : TelemetryScope
    {
        private readonly ITurnContext _turnContext;

        public ScopeDownloadFiles(ITurnContext turnContext) : base(Constants.ScopeDownloadFiles)
        {
            _turnContext = turnContext;
        }

        protected override void Callback(System.Diagnostics.Activity activity, double duration, Exception? exception)
        {
            activity.SetTag(TagNames.AttachmentCount, _turnContext.Activity.Attachments?.Count ?? 0);
        }
    }
}