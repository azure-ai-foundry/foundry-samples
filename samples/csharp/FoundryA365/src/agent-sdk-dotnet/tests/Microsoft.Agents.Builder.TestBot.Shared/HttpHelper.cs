// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.Core.Models;
using System.Text;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Builder.TestBot.Shared
{
    internal static class HttpHelper
    {
        public static Activity ReadRequest(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return ProtocolJsonSerializer.ToObject<Activity>(request.Body);
        }

        public static void WriteResponse(HttpResponse response, InvokeResponse invokeResponse)
        {
            ArgumentNullException.ThrowIfNull(response);

            if (invokeResponse == null)
            {
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                response.ContentType = "application/json";
                response.StatusCode = (int)invokeResponse.Status;

                var json = ProtocolJsonSerializer.ToJson(invokeResponse.Body);
                using var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(json));
                memoryStream.CopyTo(response.Body);
            }
        }
    }
}
