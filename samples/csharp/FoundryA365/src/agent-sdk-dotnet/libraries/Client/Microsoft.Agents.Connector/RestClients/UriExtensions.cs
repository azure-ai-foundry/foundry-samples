// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;

namespace Microsoft.Agents.Connector.RestClients
{
    public static class UriExtensions
    {
        public static Uri AppendQuery(this Uri uri, string name, string value, bool escape = true)
        {
            if (value == null)
            {
                return uri;
            }

            var argValue = escape ? Uri.EscapeDataString(value) : value;
            var url = uri.AbsoluteUri;
#if !NETSTANDARD
            if (!url.Contains('?'))
#else
            if (!url.Contains("?"))
#endif
            {
                return new Uri($"{url}?{name}={argValue}");
            }
            else
            {
                return new Uri($"{url}&{name}={argValue}");
            }
        }

        internal static Uri EnsureTrailingSlash(this Uri uri)
        {

            AssertionHelpers.ThrowIfNull(uri, nameof(uri));
            string uriString = uri.ToString();
#if !NETSTANDARD
            if (!uriString.EndsWith('/'))
#else
            if (!uriString.EndsWith("/"))
#endif
            {
                uriString += "/";
            }
            return new Uri(uriString);

        }
    }
}
