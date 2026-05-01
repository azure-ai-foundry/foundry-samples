// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies the failed entry with its id and error.
    /// </summary>
    public class BatchFailedEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.BatchFailedEntry"/> class.
        /// </summary>
        public BatchFailedEntry()
        {
        }

        /// <summary>
        /// Gets or sets the id of the failed entry.
        /// </summary>
        /// <value>The id of the failed entry.</value>
        public string EntryId { get; set; }

        /// <summary>
        /// Gets or sets the error of the failed entry.
        /// </summary>
        /// <value>The error of the failed entry.</value>
        public string Error { get; set; }
    }
}
