// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Object representing inner http error. </summary>
    public class InnerHttpError
    {
        /// <summary> Initializes a new instance of InnerHttpError. </summary>
        public InnerHttpError()
        {
        }

        /// <summary> Initializes a new instance of InnerHttpError. </summary>
        /// <param name="statusCode"> HttpStatusCode from failed request. </param>
        /// <param name="body"> Body from failed request. </param>
        public InnerHttpError(int? statusCode = default, object body = default)
        {
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary> HttpStatusCode from failed request. </summary>
        public int? StatusCode { get; set; }
        /// <summary> Body from failed request. </summary>
        public object Body { get; set; }
    }
}
