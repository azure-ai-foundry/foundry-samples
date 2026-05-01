// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Shared context to manage request headers that will be used to propagate them in the <see cref="Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationExtensions.AddHeaderPropagation"/>.
/// </summary>
public class HeaderPropagationContext()
{
    private static readonly AsyncLocal<IDictionary<string, StringValues>> _headersFromRequest = new();
    private static HeaderPropagationEntryCollection _headersToPropagate = new();

    static HeaderPropagationContext()
    {
        HeaderPropagationAssemblyAttribute.InitHeaderPropagation();
    }

    /// <summary>
    /// Gets or sets the request headers that will be propagated based on what's inside the <see cref="Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationContext.HeadersToPropagate"/> property.
    /// </summary>
    public static IDictionary<string, StringValues> HeadersFromRequest
    {
        get
        {
            return _headersFromRequest.Value;
        }
        set
        {
            // Create a copy to ensure headers are not modified by the original request.
#if !NETSTANDARD
            var headers = value?.ToDictionary(StringComparer.InvariantCultureIgnoreCase);
#else
            var headers = value?.ToDictionary(x => x.Key, x => x.Value, StringComparer.InvariantCultureIgnoreCase);
#endif
            _headersFromRequest.Value = FilterHeaders(headers);
        }
    }

    /// <summary>
    /// Gets or sets the headers to allow during the propagation.
    /// </summary>
    public static HeaderPropagationEntryCollection HeadersToPropagate
    {
        get
        {
            return _headersToPropagate;
        }
        set
        {
            _headersToPropagate = value ?? new();
        }
    }

    /// <summary>
    /// Filters the request headers based on the keys provided in <see cref="Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationContext.HeadersToPropagate"/>.
    /// </summary>
    /// <param name="requestHeaders">Headers collection from an Http request.</param>
    /// <returns>Filtered headers.</returns>
    private static Dictionary<string, StringValues> FilterHeaders(Dictionary<string, StringValues> requestHeaders)
    {
        var result = new Dictionary<string, StringValues>();

        if (requestHeaders == null || requestHeaders.Count == 0)
        {
            return result;
        }

        // Ensure the default headers are always set by overriding the LoadHeaders configuration.
        _headersToPropagate.Propagate("x-ms-correlation-id");

        foreach (var header in HeadersToPropagate.Entries)
        {
            var headerExists = requestHeaders.TryGetValue(header.Key, out var requestHeader);

            switch (header.Action)
            {
                case HeaderPropagationEntryAction.Add:
#if !NETSTANDARD
                    result.TryAdd(header.Key, header.Value);
#else
                    result.Add(header.Key, header.Value);
#endif
                    break;
                case HeaderPropagationEntryAction.Append when headerExists:
                    if (string.Equals(header.Key, "user-agent", StringComparison.OrdinalIgnoreCase))
                    {
                        // Special handling for User-Agent to concatenate values with a space.
                        result[header.Key] = new StringValues(string.Join(" ", [.. requestHeader, header.Value]));
                    }
                    else
                    {
                        // For other headers, use default concat which separate the list with a comma.
                        result[header.Key] = requestHeader.Concat(header.Value).ToArray();
                    }
                    break;
                case HeaderPropagationEntryAction.Propagate when headerExists:
                    result[header.Key] = requestHeader;
                    break;
                case HeaderPropagationEntryAction.Override:
                    result[header.Key] = header.Value;
                    break;
            }
        }

        return result;
    }
}