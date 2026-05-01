// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A tuple class containing an HTTP status code and a JSON-serializable object. </summary>
    public class InvokeResponse
    {
        /// <summary> Initializes a new instance of InvokeResponse. </summary>
        public InvokeResponse()
        {
        }

        /// <summary> Initializes a new instance of InvokeResponse. </summary>
        /// <param name="status"> The HTTP status code for the response. </param>
        /// <param name="body"> The body content for the response. </param>
        internal InvokeResponse(int? status, object body)
        {
            Status = status;
            Body = body;
        }

        /// <summary>
        /// Gets a value indicating whether the invoke response was successful.
        /// </summary>
        /// <returns>
        /// A value that indicates if the HTTP response was successful.
        /// true if <see cref="Microsoft.Agents.Core.Models.InvokeResponse.Status"/> was in the Successful range (200-299); otherwise false.
        /// </returns>
        public bool IsSuccessStatusCode() => Status >= 200 && Status <= 299;

        /// <summary> The HTTP status code for the response. </summary>
        public int? Status { get; set; }
        /// <summary> The body content for the response. </summary>
        public object Body { get; set; }
    }
}
