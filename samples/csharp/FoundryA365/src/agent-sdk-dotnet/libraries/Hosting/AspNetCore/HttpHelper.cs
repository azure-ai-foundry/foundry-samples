// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Helper class with methods to help with reading and responding to HTTP requests.
    /// </summary>
    public static class HttpHelper
    {
        /// <summary>
        /// Accepts an incoming HttpRequest and deserializes it using the <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the request into.</typeparam>
        /// <param name="request">The HttpRequest.</param>
        /// <returns>The deserialized request.</returns>
        public static async Task<T> ReadRequestAsync<T>(HttpRequest request)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(request);
                return await JsonSerializer.DeserializeAsync<T>(request.Body, ProtocolJsonSerializer.SerializationOptions).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        /// <summary>
        /// If an <see cref="Microsoft.Agents.Core.Models.InvokeResponse"/> is provided, the status and body of the <see cref="Microsoft.Agents.Core.Models.InvokeResponse"/>
        /// are used to set the status and body of the <see cref="Microsoft.AspNetCore.Http.HttpResponse"/>. If no <see cref="Microsoft.Agents.Core.Models.InvokeResponse"/>
        /// is provided, then the status of the <see cref="Microsoft.AspNetCore.Http.HttpResponse"/> is set to 200.
        /// </summary>
        /// <param name="response">A HttpResponse.</param>
        /// <param name="invokeResponse">An instance of <see cref="Microsoft.Agents.Core.Models.InvokeResponse"/>.</param>
        /// <returns>A Task representing the work to be executed.</returns>
        public static async Task WriteResponseAsync(HttpResponse response, InvokeResponse invokeResponse)
        {
            ArgumentNullException.ThrowIfNull(response);

            if (invokeResponse == null)
            {
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                response.StatusCode = (int)invokeResponse.Status;

                if (invokeResponse.Body != null)
                {
                    response.ContentType = "application/json; charset=utf-8";

                    var json = ProtocolJsonSerializer.ToJson(invokeResponse.Body);
                    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Get the <see cref="System.Security.Claims.ClaimsIdentity"/> from the <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.
        /// </summary>
        /// <param name="request">The HttpRequest.</param>
        /// <returns>The ClaimsIdentity from the request.</returns>
        public static ClaimsIdentity GetClaimsIdentity(HttpRequest request)
        {
            // If Auth is not configured, we still need the claims from the JWT token.
            // Currently, the stack does rely on certain Claims. If the Bearer token
            // was sent, we can get them from there. The JWT token is NOT validated though.

            var claimsIdentity = request.HttpContext.User?.Identity as ClaimsIdentity;

            if (claimsIdentity != null && !claimsIdentity.IsAuthenticated && !claimsIdentity.Claims.Any())
            {
                var auth = request.Headers.Authorization;
                if (auth.Count != 0)
                {
                    var authHeaderValue = auth.First();
                    var authValues = authHeaderValue.Split(' ');
                    if (authValues.Length == 2 && authValues[0].Equals("bearer", StringComparison.OrdinalIgnoreCase))
                    {
                        var jwt = new JwtSecurityToken(authValues[1]);
                        claimsIdentity = new ClaimsIdentity(jwt.Claims);
                    }
                }
            }

            return claimsIdentity;
        }
    }
}
