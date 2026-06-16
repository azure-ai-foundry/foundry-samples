// Copyright (c) Microsoft. All rights reserved.

using Azure.Core;

namespace BrowserAutomation;

/// <summary>
/// A TokenCredential wrapper that overrides the requested scope with
/// https://ai.azure.com/.default for toolbox MCP authentication.
///
/// The framework's FoundryToolboxBearerTokenHandler hardcodes
/// cognitiveservices.azure.com which some regions reject.
/// This mirrors the Python pattern where we explicitly set azure_scope.
/// </summary>
internal sealed class ToolboxScopedCredential(TokenCredential inner) : TokenCredential
{
    private static readonly TokenRequestContext ToolboxContext =
        new(["https://ai.azure.com/.default"]);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => inner.GetToken(ToolboxContext, cancellationToken);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => inner.GetTokenAsync(ToolboxContext, cancellationToken);
}
