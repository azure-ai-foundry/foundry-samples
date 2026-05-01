// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Activity with Claims which should already have been authenticated.
    /// </summary>
    public class ActivityWithClaims
    {
        /// <summary>
        /// Gets or sets the channel adapter used to send and receive messages.
        /// </summary>
        public IChannelAdapter ChannelAdapter { get; set; }

        /// <summary>
        /// Optional: Defaults to IAgent
        /// </summary>
        public Type AgentType { get; set; }

        /// <summary>
        /// <see cref="System.Security.Claims.ClaimsIdentity"/> retrieved from a call to authentication.
        /// </summary>
        public ClaimsIdentity ClaimsIdentity { get; set; }

        /// <summary>
        /// <see cref="Microsoft.Agents.Core.Models.Activity"/> to be processed.
        /// </summary>
        public IActivity Activity { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the operation should be performed proactively.
        /// </summary>
        public bool IsProactive { get; set; }

        /// <summary>
        /// Gets or sets the audience claim value used for proactive authentication requests.
        /// </summary>
        public string ProactiveAudience { get; set; }

        /// <summary>
        /// Invoked when ProcessActivity is done.  Ignored if IsProactive.
        /// </summary>
        public Func<InvokeResponse, Task> OnComplete { get; set; }

        /// <summary>
        /// Headers used for the current <see cref="Microsoft.Agents.Core.Models.Activity"/> request.
        /// </summary>
        public IHeaderDictionary Headers { get; set; }

        /// <summary>
        /// Holds the <see cref="System.Diagnostics.Activity"/> used for distributed tracing and telemetry
        /// correlation, cloned from the original request context to enable trace continuity across the
        /// background queue boundary.
        /// </summary>
        public System.Diagnostics.Activity TelemetryActivity { get; set; }
    }
}
