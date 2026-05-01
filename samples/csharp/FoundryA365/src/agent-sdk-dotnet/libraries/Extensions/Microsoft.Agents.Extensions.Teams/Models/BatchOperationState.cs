// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;


namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Object representing operation state.
    /// </summary>
    public class BatchOperationState
    {
        /// <summary>
        /// Gets or sets the operation state.
        /// </summary>
        /// <value>
        /// The operation state.
        /// </value>
        public string State { get; set; }

        /// <summary>
        /// Gets the status map of the operation.
        /// </summary>
        /// <value>
        /// The status map for processed users.
        /// </value>
        public IDictionary<int, int> StatusMap { get; } = new Dictionary<int, int>();

        /// <summary>
        /// Gets or sets the datetime value to retry the operation.
        /// </summary>
        /// <value>
        /// The datetime value to retry the operation.
        /// </value>
        public DateTime? RetryAfter { get; set; }

        /// <summary>
        /// Gets or sets the total number of entries.
        /// </summary>
        /// <value>
        /// The number of entries.
        /// </value>
        public int TotalEntriesCount { get; set; }
    }
}
