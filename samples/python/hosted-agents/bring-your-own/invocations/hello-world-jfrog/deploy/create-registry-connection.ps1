<#
.SYNOPSIS
    Creates the Microsoft Foundry registry connection used to pull images from a
    private JFrog Artifactory registry via Entra workload identity federation.

.DESCRIPTION
    Run this ONCE per Foundry project. The connection stores only non-secret OIDC
    configuration (audience, token endpoint, provider name).

    Never place a JFrog password, API key, reference token, or Docker "auth" value
    in this connection. Foundry exchanges a short-lived Entra token at pull time.

    TEMPORARY: creating this connection through the REST API is a stopgap. azd
    cannot declare a registry connection today. The team is working to make this
    completely deployable via azd, after which the connection will be declared in
    azure.yaml and this script will be removed.

.EXAMPLE
    ./create-registry-connection.ps1 `
        -SubscriptionId  00000000-0000-0000-0000-000000000000 `
        -ResourceGroup   my-rg `
        -AccountName     my-foundry-account `
        -ProjectName     my-project `
        -JFrogHost       mytenant.jfrog.io `
        -JFrogRepository docker-local `
        -OidcAudience    11111111-1111-1111-1111-111111111111 `
        -OidcProviderName my-entra-provider
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][string]$ResourceGroup,
    [Parameter(Mandatory)][string]$AccountName,
    [Parameter(Mandatory)][string]$ProjectName,
    [Parameter(Mandatory)][string]$JFrogHost,
    [Parameter(Mandatory)][string]$JFrogRepository,
    # Application (client) ID of the Entra app registration used as the token audience.
    [Parameter(Mandatory)][string]$OidcAudience,
    # Name of the OIDC provider configured in JFrog.
    [Parameter(Mandatory)][string]$OidcProviderName,
    [string]$ConnectionName = 'jfrog-oidc-registry'
)

$ErrorActionPreference = 'Stop'

$target        = "https://$JFrogHost"
$tokenEndpoint = "https://$JFrogHost/access/api/v1/oidc/token"

# The CustomKeys credential type requires at least one key, so the non-secret OIDC
# settings are stored there as well as in metadata.
$payload = @{
    properties = @{
        authType      = 'CustomKeys'
        category      = 'CustomKeys'
        target        = $target
        isSharedToAll = $false
        credentials   = @{
            keys = @{
                audience      = $OidcAudience
                tokenEndpoint = $tokenEndpoint
                providerName  = $OidcProviderName
            }
        }
        metadata = @{
            type          = 'registry_connection'
            mode          = 'OAuthTokenExchange'
            audience      = $OidcAudience
            tokenEndpoint = $tokenEndpoint
            providerName  = $OidcProviderName
            registryHost  = $JFrogHost
            repository    = $JFrogRepository
        }
    }
} | ConvertTo-Json -Depth 10

$bodyFile = Join-Path ([IO.Path]::GetTempPath()) 'foundry-registry-connection.json'
[IO.File]::WriteAllText($bodyFile, $payload)

$uri = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup" +
       "/providers/Microsoft.CognitiveServices/accounts/$AccountName/projects/$ProjectName" +
       "/connections/$ConnectionName" + '?api-version=2025-10-01-preview'

Write-Host "Creating registry connection '$ConnectionName' -> $target ..."
az rest --method put --url $uri --headers 'Content-Type=application/json' --body "@$bodyFile" -o json

Remove-Item $bodyFile -Force
Write-Host "Done. Reference this connection by NAME ('$ConnectionName') when creating an agent version."
