// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class HeaderPropagationMiddlewareTests
    {
        /// <summary>
        /// Ensures that the static <see cref="AsyncLocal{T}"/> property <see cref="HeaderPropagationContext.HeadersFromRequest"/>
        /// remains isolated and unaffected when accessed by different contexts, such as HTTP requests, threads, or asynchronous flows.
        /// </summary>
        [Fact]
        public async Task HeaderPropagationContext_ShouldMaintainIsolation()
        {
            // Assign headers to propagate in the current context.
            HeaderPropagationContext.HeadersToPropagate.Propagate("First");
            HeaderPropagationContext.HeadersToPropagate.Propagate("Second");

            // Assign request headers to the current context.
            HeaderPropagationContext.HeadersFromRequest = new HeaderDictionary
            {
                { "First", "first value" },
                { "Second", "second value" }
            };

            // Create multiple requests with different contexts.
            var requests = await SimulateIncomingRequestsAsync(10);
            foreach (var (headerName, headerValue, headersFromRequest) in requests)
            {
                Assert.Single(headersFromRequest);
                Assert.True(headersFromRequest.TryGetValue(headerName, out var headerFromRequestValue));
                Assert.Equal(headerValue, headerFromRequestValue);
            }

            // Despite performing multiple requests in the middle of the current context, they did not interfere.
            Assert.Equal(2, HeaderPropagationContext.HeadersFromRequest.Count);
            Assert.True(HeaderPropagationContext.HeadersFromRequest.TryGetValue("First", out var _));
            Assert.True(HeaderPropagationContext.HeadersFromRequest.TryGetValue("Second", out var _));
        }

        /// <summary>
        /// Simulates multiple incoming requests to test the header propagation functionality.
        /// </summary>
        /// <remarks>
        /// A new <see cref="HttpContext"/> instance is created for each request, simulating the behavior of an ASP.NET Core application.
        /// More information can be found <a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/use-http-context">here</a>.
        /// <br/><br/>
        /// Additionally, the <see cref="HeaderPropagationContext"/> uses a static property alongside the <see cref="AsyncLocal{T}"/> functionality to ensure that each request's headers are stored separately and do not interfere with each other.
        /// </remarks>
        /// <param name="count">Number of incoming requests to create.</param>
        /// <returns>A Task collection with header name, value, and headers from the request.</returns>
        private static Task<(string, string, IDictionary<string, StringValues>)[]> SimulateIncomingRequestsAsync(int count)
        {
            var middleware = new HeaderPropagationMiddleware((_) => Task.CompletedTask);
            var tasks = new List<Task<(string, string, IDictionary<string, StringValues>)>>();

            for (int i = 0; i < count; i++)
            {
                var task = Task.Run(async () =>
                {
                    var headerName = $"X-Test-Header-{i}";
                    var headerValue = $"Test-Value-{i}";
                    var context = new DefaultHttpContext();

                    HeaderPropagationContext.HeadersToPropagate.Propagate(headerName);
                    context.Request.Headers.Append(headerName, headerValue);

                    await middleware.Invoke(context);
                    return (headerName, headerValue, HeaderPropagationContext.HeadersFromRequest);
                });
                tasks.Add(task);
            }

            return Task.WhenAll(tasks);
        }

    }
}