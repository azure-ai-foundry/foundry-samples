// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable MAAI001 // HostedSessionIsolationKeyProvider is an experimental Agents AI API.

using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Microsoft.Agents.AI.Foundry.Hosting;

namespace SampleApp;

/// <summary>
/// Local-development <see cref="HostedSessionIsolationKeyProvider"/> that falls back to a fixed
/// user id when the platform does not inject the <c>x-agent-user-id</c> header. Harness agents
/// carry per-session state (todo, mode, memory) and therefore require an isolation key; without
/// this fallback, running locally (dotnet run / azd ai agent run / Inspector) fails with a 500.
/// In hosted Foundry environments the platform-injected header takes precedence.
/// </summary>
internal sealed class LocalDevSessionIsolationKeyProvider : HostedSessionIsolationKeyProvider
{
    private const string LocalDevUserId = "local-dev-user";

    public override ValueTask<HostedSessionContext?> GetKeysAsync(
        ResponseContext context,
        CreateResponse request,
        CancellationToken cancellationToken)
    {
        var userKey = context?.PlatformContext?.UserIdKey;
        var userId = string.IsNullOrWhiteSpace(userKey) ? LocalDevUserId : userKey!;
        return new ValueTask<HostedSessionContext?>(new HostedSessionContext(userId));
    }
}
