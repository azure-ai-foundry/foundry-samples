#!/usr/bin/env pwsh
param(
    [string]$ProjectEndpoint = $env:AZURE_AI_PROJECT_ENDPOINT,
    [string]$AgentName = $env:AGENT_NAME,
    [string]$TenantId = $env:TENANT_ID
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectEndpoint)) {
    throw "AZURE_AI_PROJECT_ENDPOINT is required."
}

if ([string]::IsNullOrWhiteSpace($AgentName)) {
    throw "AGENT_NAME is required."
}

if ([string]::IsNullOrWhiteSpace($TenantId)) {
    throw "TENANT_ID is required."
}

$accessToken = az account get-access-token `
    --resource https://ai.azure.com `
    --query accessToken `
    --output tsv `
    --tenant $TenantId

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw "Failed to acquire an access token for https://ai.azure.com."
}

$headers = @{
    "Accept"           = "application/json"
    "Authorization"    = "Bearer $accessToken"
    "Foundry-Features" = "HostedAgents=V1Preview"
}

$escapedAgentName = [Uri]::EscapeDataString($AgentName)
$sessionsUrl = "$($ProjectEndpoint.TrimEnd('/'))/agents/$escapedAgentName/endpoint/sessions"
$activeSessions = [System.Collections.Generic.List[object]]::new()
$paginationToken = $null

do {
    $listUrl = "${sessionsUrl}?api-version=v1&limit=100"
    if (-not [string]::IsNullOrWhiteSpace($paginationToken)) {
        $listUrl += "&pagination_token=$([Uri]::EscapeDataString($paginationToken))"
    }

    try {
        $response = Invoke-RestMethod `
            -Uri $listUrl `
            -Method Get `
            -Headers $headers `
            -ErrorAction Stop
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            Write-Host "Agent '$AgentName' does not exist yet; no sessions to stop."
            return
        }

        throw
    }

    foreach ($session in @($response.data)) {
        if ($session.status -eq "active" -and
            -not [string]::IsNullOrWhiteSpace($session.agent_session_id)) {
            $activeSessions.Add($session)
        }
    }

    $paginationToken = $response.pagination_token
} while (-not [string]::IsNullOrWhiteSpace($paginationToken))

if ($activeSessions.Count -eq 0) {
    Write-Host "No active sessions found for agent '$AgentName'."
    return
}

foreach ($session in $activeSessions) {
    $sessionId = $session.agent_session_id
    $escapedSessionId = [Uri]::EscapeDataString($sessionId)
    $stopUrl = "${sessionsUrl}/${escapedSessionId}:stop?api-version=v1"

    Write-Host "Stopping active session '$sessionId'..."
    Invoke-RestMethod `
        -Uri $stopUrl `
        -Method Post `
        -Headers $headers `
        -ErrorAction Stop | Out-Null
}

Write-Host "Stopped $($activeSessions.Count) active session(s) for agent '$AgentName'."
