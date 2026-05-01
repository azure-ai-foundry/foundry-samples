// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Describes a tenant.
    /// </summary>
    public class TenantInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TenantInfo"/> class.
        /// </summary>
        public TenantInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TenantInfo"/> class.
        /// </summary>
        /// <param name="id">Unique identifier representing a tenant.</param>
        public TenantInfo(string id = default)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets unique identifier representing a tenant.
        /// </summary>
        /// <value>The ID representing a tenant.</value>
        public string Id { get; set; }
    }
}
