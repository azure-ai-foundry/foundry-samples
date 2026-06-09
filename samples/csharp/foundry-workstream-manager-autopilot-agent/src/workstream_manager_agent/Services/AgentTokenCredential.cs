namespace WorkstreamManager.Services;

using System.IdentityModel.Tokens.Jwt;
using Azure.Core;
using WorkstreamManager.Models;

/// <summary>
/// TokenCredential implementation that calls AgentTokenHelper to acquire tokens.
/// Includes token caching and expiry handling with thread-safe token refresh.
/// </summary>
public class AgentTokenCredential(AgentTokenHelper agentTokenHelper, AgentMetadata agent) : TokenCredential
{
    private AccessToken? cachedToken;
    private readonly SemaphoreSlim tokenSemaphore = new(1, 1);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (cachedToken.HasValue && DateTimeOffset.UtcNow.AddMinutes(5) < cachedToken.Value.ExpiresOn)
        {
            return cachedToken.Value;
        }

        await tokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (cachedToken.HasValue && DateTimeOffset.UtcNow.AddMinutes(5) < cachedToken.Value.ExpiresOn)
            {
                return cachedToken.Value;
            }

            var scopes = requestContext.Scopes.Length > 0
                ? requestContext.Scopes
                : ["https://canary.graph.microsoft.com/.default"];

            var token = await agentTokenHelper.GetAgenticUserTokenAsync(
                agent.AgentApplicationId.ToString(),
                agent.AgentId.ToString(),
                agent.UserId.ToString(),
                agent.TenantId.ToString(),
                scopes);

            var expiresOn = GetTokenExpiryTime(token);
            var accessToken = new AccessToken(token, expiresOn);

            cachedToken = accessToken;
            return accessToken;
        }
        finally
        {
            tokenSemaphore.Release();
        }
    }

    private static DateTimeOffset GetTokenExpiryTime(string token)
    {
        try
        {
            if (new JwtSecurityTokenHandler().CanReadToken(token))
            {
                var jwtToken = new JwtSecurityToken(token);
                return jwtToken.ValidTo;
            }
        }
        catch
        {
            // If parsing fails, default to 1 hour from now
        }

        return DateTimeOffset.UtcNow.AddHours(1);
    }
}

