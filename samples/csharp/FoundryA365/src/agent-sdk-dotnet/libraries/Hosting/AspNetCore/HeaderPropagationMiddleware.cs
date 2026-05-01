// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Hosting.AspNetCore;

/// <summary>
/// A middleware to propagate incoming request headers to outgoing ones by internally using the <see cref="Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationContext"/> static class.
/// </summary>
public class HeaderPropagationMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of <see cref="Microsoft.Agents.Hosting.AspNetCore.HeaderPropagationMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public HeaderPropagationMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        _next = next;
    }

    /// <summary>
    /// Executes the middleware to set the request headers in <see cref="Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationContext"/>.
    /// </summary>
    /// <param name="context">The <see cref="Microsoft.AspNetCore.Http.HttpContext"/> for the current request.</param>
    public Task Invoke(HttpContext context)
    {
        HeaderPropagationContext.HeadersFromRequest = context.Request.Headers;

        return _next.Invoke(context);
    }
}