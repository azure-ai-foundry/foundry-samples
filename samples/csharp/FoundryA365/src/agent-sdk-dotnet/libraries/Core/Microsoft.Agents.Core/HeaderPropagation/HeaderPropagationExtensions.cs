// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.Agents.Core.HeaderPropagation;

public static class HeaderPropagationExtensions
{
    /// <summary>
    /// Loads incoming request headers based on a list of headers to propagate into the HttpClient.
    /// </summary>
    /// <param name="httpClient">The <see cref="System.Net.Http.HttpClient"/>.</param>
    public static void AddHeaderPropagation(this HttpClient httpClient)
    {
        if (HeaderPropagationContext.HeadersFromRequest == null)
        {
            return;
        }

        foreach (var header in HeaderPropagationContext.HeadersFromRequest)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, [header.Value]);
        }
    }
}
