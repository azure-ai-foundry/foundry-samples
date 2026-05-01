// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> The AadResourceUrls. </summary>
    public class AadResourceUrls
    {
        /// <summary> Initializes a new instance of AadResourceUrls. </summary>
        public AadResourceUrls()
        {
            ResourceUrls = [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.AadResourceUrls"/> class.
        /// </summary>
        /// <param name="resourceUrls">The URLs to the resource you want to connect to.</param>
        public AadResourceUrls(IList<string> resourceUrls = default)
        {
            ResourceUrls = resourceUrls ?? [];
        }

        /// <summary> Gets the resource urls. </summary>
        public IList<string> ResourceUrls { get; set; }
    }
}
