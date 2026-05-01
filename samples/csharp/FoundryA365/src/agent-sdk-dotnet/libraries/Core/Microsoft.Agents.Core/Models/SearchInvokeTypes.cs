// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines values for SearchInvokeTypes. See <see cref="Microsoft.Agents.Core.Models.SearchInvokeValue"/>.
    /// </summary>
    public static class SearchInvokeTypes
    {
        /// <summary>
        /// The type for Search.
        /// Implies a standard, paginated search operation that expects
        /// one or more templated results to be returned.
        /// </summary>
        public const string Search = "search";

        /// <summary>
        /// The type for Agent SearchAnswer.
        /// Implies a simpler search that does not include pagination,
        /// and most typically only returns a single search result.
        /// </summary>
        public const string SearchAnswer = "searchAnswer";

        /// <summary>
        /// The type for Typeahead.
        /// Implies a search for a small set of values, most often used
        /// for dynamic auto-complete or type-ahead UI controls.
        /// This search supports pagination.
        /// </summary>
        public const string Typeahead = "typeahead";
    }
}
