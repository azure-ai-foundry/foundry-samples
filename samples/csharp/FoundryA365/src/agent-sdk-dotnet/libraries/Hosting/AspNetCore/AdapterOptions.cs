// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// 
    /// </summary>
    public class AdapterOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of seconds to wait for the application to shut down gracefully. 
        /// </summary>
        /// <remarks>If the shutdown process does not complete within the specified timeout, the
        /// application may be terminated forcefully. Set this value according to the expected shutdown duration of your
        /// application components.</remarks>
        public int ShutdownTimeoutSeconds { get; set; } = 60;
    }
}
