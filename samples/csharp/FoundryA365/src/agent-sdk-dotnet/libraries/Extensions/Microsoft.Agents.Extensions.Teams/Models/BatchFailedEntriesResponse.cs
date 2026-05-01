// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies the failed entries response.
    /// Contains a list of <see cref="Microsoft.Agents.Extensions.Teams.Models.BatchFailedEntry"/>.
    /// </summary>
    public class BatchFailedEntriesResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.BatchFailedEntriesResponse"/> class.
        /// </summary>
        public BatchFailedEntriesResponse()
        {
        }

        /// <summary>
        /// Gets or sets the continuation token for paginated results.
        /// </summary>
        /// <value>The continuation token for paginated results.</value>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Gets the list of failed entries result of a batch operation.
        /// </summary>
        /// <value>The list of failed entries result of a batch operation.</value>
        public IList<BatchFailedEntry> FailedEntries { get; set; } = [];
    }
}
