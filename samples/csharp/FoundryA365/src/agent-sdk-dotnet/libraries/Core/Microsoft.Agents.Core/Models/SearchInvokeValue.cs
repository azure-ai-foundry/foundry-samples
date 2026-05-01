// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Defines the structure that arrives in the Activity.Value for Invoke activity with Name of 'application/search'. </summary>
    public class SearchInvokeValue
    {
        /// <summary> Initializes a new instance of SearchInvokeValue. </summary>
        public SearchInvokeValue()
        {
        }

        /// <summary> Initializes a new instance of SearchInvokeValue. </summary>
        /// <param name="kind"> The kind for this search invoke action value. </param>
        /// <param name="queryText"> The query text of this search invoke action value. </param>
        /// <param name="queryOptions"> The query options for the query. </param>
        /// <param name="context"> The context information about the query. </param>
        internal SearchInvokeValue(string kind, string queryText, SearchInvokeOptions queryOptions, object context)
        {
            Kind = kind;
            QueryText = queryText;
            QueryOptions = queryOptions;
            Context = context;
        }

        /// <summary> The kind for this search invoke action value. </summary>
        public string Kind { get; set; }
        /// <summary> The query text of this search invoke action value. </summary>
        public string QueryText { get; set; }
        /// <summary> The query options for the query. </summary>
        public SearchInvokeOptions QueryOptions { get; set; }
        /// <summary> The context information about the query. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Context { get; set; }
    }
}
