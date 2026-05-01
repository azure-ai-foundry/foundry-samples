// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Combination of String, Regex, and RouteSelectorAsync selectors.
    /// </summary>
    public class MultipleRouteSelector
    {
        /// <summary>
        /// The string selectors.
        /// </summary>
        public string[]? Strings { get; set; }

        /// <summary>
        /// The Regex selectors.
        /// </summary>
        public Regex[]? Regexes { get; set; }

        /// <summary>
        /// The RouteSelectorAsync function selectors. 
        /// </summary>
        public RouteSelector[]? RouteSelectors { get; set; }
    }
}
