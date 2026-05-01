// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Defines the structure that is returned as the result of an Invoke activity with Name of 'adaptiveCard/search'. </summary>
    public class SearchInvokeResponse
    {
        /// <summary> Initializes a new instance of SearchInvokeResponse. </summary>
        public SearchInvokeResponse()
        {
        }

        /// <summary> Initializes a new instance of SearchInvokeResponse. </summary>
        /// <param name="statusCode"> The Card Action response StatusCode. </param>
        /// <param name="type"> The Type of this response. </param>
        /// <param name="value"> The json response object. </param>
        internal SearchInvokeResponse(int? statusCode, string type, object value)
        {
            StatusCode = statusCode;
            Type = type;
            Value = value;
        }

        /// <summary> The Card Action response StatusCode. </summary>
        public int? StatusCode { get; set; }
        /// <summary> The Type of this response. </summary>
        public string Type { get; set; }
        /// <summary> The json response object. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Value { get; set; }
    }
}
