// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication
{
    /// <summary>
    /// Optional interface for a token provider that support OBO token exchanges.
    /// </summary>
    public interface IOBOExchange
    {
        Task<TokenResponse> AcquireTokenOnBehalfOf(IEnumerable<string> scopes, string token);
    }
}
