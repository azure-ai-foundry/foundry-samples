#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)]
    [string]$AgentName,
    [string]$Environment
)

$ErrorActionPreference = "Stop"
$terminalStatuses = @("idle", "deleting", "deleted", "expired")
$environmentArgs = @()

if (-not [string]::IsNullOrWhiteSpace($Environment)) {
    $environmentArgs = @("--environment", $Environment)
}

$sessionJson = & azd ai agent sessions list `
    --agent-name $AgentName `
    --output json `
    @environmentArgs

if ($LASTEXITCODE -ne 0) {
    throw "Failed to list sessions for agent '$AgentName'."
}

$response = $sessionJson | ConvertFrom-Json
$sessions = @($response.data)

if ($sessions.Count -eq 0) {
    Write-Host "No existing sessions found for agent '$AgentName'."
    exit 0
}

$stoppedCount = 0
foreach ($session in $sessions) {
    $sessionId = $session.agent_session_id
    $status = if ($session.status) { $session.status } else { "unknown" }
    $version = if ($session.version_indicator.agent_version) {
        $session.version_indicator.agent_version
    }
    else {
        "unknown"
    }

    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        continue
    }

    if ($terminalStatuses -contains $status.ToLowerInvariant()) {
        Write-Host (
            "Skipping session {0} (status: {1}, version: {2})." -f
            $sessionId, $status, $version
        )
        continue
    }

    Write-Host (
        "Stopping session {0} (status: {1}, version: {2})..." -f
        $sessionId, $status, $version
    )

    & azd ai agent sessions stop $sessionId `
        --agent-name $AgentName `
        --no-prompt `
        @environmentArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to stop session '$sessionId'."
    }

    $stoppedCount++
}

Write-Host "Stopped $stoppedCount session(s) for agent '$AgentName'."
