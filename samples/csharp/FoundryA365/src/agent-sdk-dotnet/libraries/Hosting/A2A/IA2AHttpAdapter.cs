// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// An A2A Adapter using Http.
/// </summary>
public interface IA2AHttpAdapter : IAgentHttpAdapter
{
    Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default);
}
