<#
.SYNOPSIS
    Creates a hosted agent version that pulls its image from a private JFrog
    registry, and routes the agent endpoint to that version.

.DESCRIPTION
    This script covers the one step azd cannot do yet: setting
    `container_configuration.registry_connection_id` so Foundry knows which
    connection authorizes the image pull.

    Run it AFTER `azd deploy` has created the agent.

    TEMPORARY: this REST API step is a stopgap. The team is working to make the
    sample completely deployable via azd; once registry connections are supported
    in azure.yaml, `azd deploy` will handle this and the script will be removed.

    NOTE: `registry_connection_id` takes the connection NAME, not its full ARM
    resource ID. Passing the ARM ID returns an "invalid_payload" error.

.EXAMPLE
    ./create-agent-version.ps1 `
        -ProjectEndpoint https://my-account.services.ai.azure.com/api/projects/my-project `
        -AgentName       hello-world-jfrog-invocations `
        -Image           mytenant.jfrog.io/docker-local/hello-world-jfrog-invocations:1.0.0 `
        -ModelDeployment gpt-5.4-mini
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectEndpoint,
    [Parameter(Mandatory)][string]$AgentName,
    [Parameter(Mandatory)][string]$Image,
    [Parameter(Mandatory)][string]$ModelDeployment,
    [string]$ConnectionName = 'jfrog-oidc-registry',
    [string]$Cpu    = '0.5',
    [string]$Memory = '1Gi',
    [int]$TimeoutMinutes = 15
)

$ErrorActionPreference = 'Stop'
$base = $ProjectEndpoint.TrimEnd('/')

# --- 1. Create the version, attaching the registry connection -----------------
$definition = @{
    definition = @{
        kind = 'hosted'
        container_configuration = @{
            image                  = $Image
            registry_connection_id = $ConnectionName   # connection NAME, not ARM ID
        }
        cpu               = $Cpu
        memory            = $Memory
        protocol_versions = @(@{ protocol = 'invocations'; version = '2.0.0' })
        environment_variables = @{ AZURE_AI_MODEL_DEPLOYMENT_NAME = $ModelDeployment }
    }
    description = 'Image pulled from private JFrog registry via Entra OIDC token exchange'
} | ConvertTo-Json -Depth 12

$verFile = Join-Path ([IO.Path]::GetTempPath()) 'foundry-agent-version.json'
[IO.File]::WriteAllText($verFile, $definition)

Write-Host "Creating agent version for '$AgentName' ..."
$created = az rest --method post --resource 'https://ai.azure.com' `
    --url "$base/agents/$AgentName/versions?api-version=v1" `
    --headers 'Content-Type=application/json' --body "@$verFile" -o json | ConvertFrom-Json
Remove-Item $verFile -Force

$version = $created.version
Write-Host "Created version $version (status: $($created.status))"

# --- 2. Wait for the version to finish provisioning --------------------------
# A version can report "active" before the container is actually ready, so also
# require container_protocol_versions to be non-empty.
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 20
    $v = az rest --method get --resource 'https://ai.azure.com' `
        --url "$base/agents/$AgentName/versions/$version`?api-version=v1" -o json | ConvertFrom-Json

    $ready = $v.status -eq 'active' -and $v.definition.container_protocol_versions.Count -gt 0
    Write-Host "  status=$($v.status) protocols=$($v.definition.container_protocol_versions.Count)"

    if ($v.status -eq 'failed') { throw "Provisioning failed: $($v.error.code) $($v.error.message)" }
    if ($ready) { break }
}

# --- 3. Route the endpoint to this version -----------------------------------
# Required: the endpoint defaults to the "responses" protocol even when the
# version declares "invocations".
$patch = @{
    agent_endpoint = @{
        version_selector = @{
            version_selection_rules = @(
                @{ agent_version = "$version"; traffic_percentage = 100; type = 'FixedRatio' }
            )
        }
        protocol_configuration = @{ invocations = @{} }
    }
} | ConvertTo-Json -Depth 10

$patchFile = Join-Path ([IO.Path]::GetTempPath()) 'foundry-agent-endpoint.json'
[IO.File]::WriteAllText($patchFile, $patch)

Write-Host "Routing endpoint to version $version ..."
az rest --method patch --resource 'https://ai.azure.com' `
    --url "$base/agents/$AgentName`?api-version=v1" `
    --headers 'Content-Type=application/merge-patch+json' --body "@$patchFile" -o none
Remove-Item $patchFile -Force

Write-Host "Done. Invoke with: azd ai agent invoke `"What is Microsoft Foundry?`""
