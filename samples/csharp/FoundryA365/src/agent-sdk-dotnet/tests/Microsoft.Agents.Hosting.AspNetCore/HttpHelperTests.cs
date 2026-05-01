// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class HttpHelperTests
    {
        [Fact]
        public async Task WriteResponseAsync_ShouldThrowWithNullResponse()
        {
            var response = new InvokeResponse();

            await Assert.ThrowsAsync<ArgumentNullException>(() => HttpHelper.WriteResponseAsync(null, response));
        }

        [Fact]
        public async Task WriteResponseAsync_ShouldSetStatus200OK()
        {
            var context = new DefaultHttpContext();
            await HttpHelper.WriteResponseAsync(context.Response, null);
        
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }
}