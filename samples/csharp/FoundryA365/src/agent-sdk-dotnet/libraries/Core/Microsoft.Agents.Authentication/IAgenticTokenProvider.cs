// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication
{
    public interface IAgenticTokenProvider
    {
        Task<string> GetAgenticApplicationTokenAsync(string tenantId, string agentAppInstanceId, CancellationToken cancellationToken = default);

        Task<string> GetAgenticInstanceTokenAsync(string tenantId, string agentAppInstanceId, CancellationToken cancellationToken = default);

        Task<string> GetAgenticUserTokenAsync(string tenantId, string agentAppInstanceId, string upn, IList<string> scopes, CancellationToken cancellationToken = default);
    }
}
