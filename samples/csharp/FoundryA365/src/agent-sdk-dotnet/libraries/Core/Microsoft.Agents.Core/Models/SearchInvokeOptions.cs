// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines the query options in the <see cref="Microsoft.Agents.Core.Models.SearchInvokeValue"/> for Invoke activity with Name of 'application/search'.
    /// </summary>
    public class SearchInvokeOptions
    {
        /// <summary>
        /// Gets or sets the starting reference number from which ordered search results should be returned.
        /// </summary>
        /// <value>
        /// The starting reference number from which ordered search results should be returned.
        /// </value>
        public int Skip { get; set; }

        /// <summary>
        /// Gets or sets the number of search results that should be returned.
        /// </summary>
        /// <value>
        /// The number of search results that should be returned.
        /// </value>
        public int Top { get; set; }
    }
}
