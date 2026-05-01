// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using System;
using System.Net.Http;
using System.Threading;

namespace Microsoft.Agents.Connector.RestClients
{
    internal static class RestClientExceptionHelper
    {
        /// <summary>
        /// Common handler for Non Successful responses.
        /// </summary>
        /// <param name="httpResponse"></param>
        /// <param name="errorMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="errors"></param>
        internal static Exception CreateErrorResponseException(HttpResponseMessage httpResponse, AgentErrorDefinition errorMessage, CancellationToken cancellationToken, params string[] errors)
        {
            AssertionHelpers.ThrowIfNull(httpResponse, nameof(httpResponse));

            var ex = ErrorResponseException.CreateErrorResponseException(httpResponse, errorMessage, cancellationToken: cancellationToken, errors: errors);

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                    ErrorHelper.InvalidAccessTokenForAgentCallback, ex);
            }
            return ex;
        }
    }
}
