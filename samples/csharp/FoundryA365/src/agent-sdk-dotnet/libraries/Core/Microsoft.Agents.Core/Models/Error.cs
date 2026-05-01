// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Object representing error information. </summary>
    public class Error
    {
        public const string ConsentRequiredCode = "ConsentRequired";

        /// <summary> Initializes a new instance of Error. </summary>
        public Error()
        {
        }

        /// <summary> Initializes a new instance of Error. </summary>
        /// <param name="code"> Error code. </param>
        /// <param name="message"> Error message. </param>
        /// <param name="innerHttpError"> Object representing inner http error. </param>
        public Error(string code = default, string message = default, InnerHttpError innerHttpError = default)
        {
            Code = code;
            Message = message;
            InnerHttpError = innerHttpError;
        }

        /// <summary> Error code. </summary>
        public string Code { get; set; }
        /// <summary> Error message. </summary>
        public string Message { get; set; }
        /// <summary> Object representing inner http error. </summary>
        public InnerHttpError InnerHttpError { get; set; }
    }
}
