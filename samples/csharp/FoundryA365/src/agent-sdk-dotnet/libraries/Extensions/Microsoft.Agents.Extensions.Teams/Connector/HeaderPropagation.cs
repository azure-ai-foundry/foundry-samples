// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.HeaderPropagation;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    [HeaderPropagation]

#if !NETSTANDARD
    internal class HeaderPropagation : IHeaderPropagationAttribute
#else
    internal class HeaderPropagation
#endif
    {
        public static void LoadHeaders(HeaderPropagationEntryCollection collection)
        {
            // Propagate headers to the outgoing request by adding them to the HeaderPropagationEntryCollection.
        }
    }
}
