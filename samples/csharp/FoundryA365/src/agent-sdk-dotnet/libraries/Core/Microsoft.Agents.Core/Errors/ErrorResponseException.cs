// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Microsoft.Agents.Core.Errors
{
    /// <summary>
    /// Exception thrown for an invalid response with ErrorResponse
    /// information.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ErrorResponseException"/> class.
    /// </remarks>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public class ErrorResponseException(string message, System.Exception innerException = null) : Exception(message, innerException)
    {
        /// <summary>
        /// List of header names that should not be included in exception data to prevent information disclosure.
        /// </summary>
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Set-Cookie",
            "Cookie",
            "X-API-Key",
            "X-Auth-Token",
            "X-CSRF-Token",
            "WWW-Authenticate",
            "Proxy-Authorization",
            "Proxy-Authenticate"
        };

        public int? StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the body object.
        /// </summary>
        /// <value>The body.</value>
        public ErrorResponse Body { get; set; }

        public static ErrorResponseException CreateErrorResponseException(AgentErrorDefinition message, System.Exception innerException = null, params string[] errors) 
        {
            string errorMessageToSend = string.Empty;
            if (errors != null && errors.Length > 0)
            {
                errorMessageToSend = string.Format(message.description, errors);
            }
            else
            {
                errorMessageToSend = message.description;
            }

            var excecp = new ErrorResponseException(errorMessageToSend, innerException)
            {
                HResult = message.code,
                HelpLink = message.helplink
            };
            return excecp;
        }

        public static ErrorResponseException CreateErrorResponseException(HttpResponseMessage httpResponse, AgentErrorDefinition message, System.Exception innerException = null, CancellationToken cancellationToken = default, params string[] errors)
        {
            var ex = CreateErrorResponseException(message, innerException, errors);
            ex.StatusCode = (int) httpResponse.StatusCode;
            try
            {
                // Report headers in the exception Data, excluding sensitive headers
                if (httpResponse.Headers != null)
                {
                    foreach (var header in httpResponse.Headers)
                    {
                        if (!IsSensitiveHeader(header.Key))
                        {
                            ex.Data[header.Key] = string.Join(",", header.Value);
                        }
                    }
                }

#if !NETSTANDARD
                string responseContent = httpResponse.Content?.ReadAsStringAsync(cancellationToken).Result;
#else
                string responseContent = httpResponse.Content?.ReadAsStringAsync().Result;
#endif
                if (!string.IsNullOrEmpty(responseContent))
                {
                    ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(responseContent);
                    if (errorBody != null && errorBody.Error != null)
                    {
                        ex.Body = errorBody;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(errorBody?.ToString()))
                        {
                            // try to get just the error message from the response
                            Error error = ProtocolJsonSerializer.ToObject<Error>(responseContent);
                            if (error != null && error.Message != null)
                            {
                                errorBody = new ErrorResponse(error);
                                ex.Body = errorBody;
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore the exception
            }
            return ex; 
        }

        /// <summary>
        /// Determines whether a header name is considered sensitive and should not be included in exception data.
        /// </summary>
        /// <param name="headerName">The name of the header to check.</param>
        /// <returns>True if the header is sensitive and should be filtered; otherwise, false.</returns>
        private static bool IsSensitiveHeader(string headerName)
        {
            return SensitiveHeaders.Contains(headerName);
        }
    }
}
