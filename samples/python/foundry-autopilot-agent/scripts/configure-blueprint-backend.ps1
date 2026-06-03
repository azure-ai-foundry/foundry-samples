#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

Write-Host "Configuring blueprint backend configuration in Teams Developer Portal..."

$blueprintId = $env:AGENT_IDENTITY_BLUEPRINT_ID
if ([string]::IsNullOrEmpty($blueprintId)) {
    throw "AGENT_IDENTITY_BLUEPRINT_ID environment variable is not set."
}

# The Teams Developer Portal API expects a token with audience https://dev.teams.microsoft.com.
# If this fails, run: az login --scope https://dev.teams.microsoft.com/.default
$token = az account get-access-token --resource https://dev.teams.microsoft.com --query accessToken -o tsv
if ([string]::IsNullOrEmpty($token)) {
    throw "Failed to acquire access token for https://dev.teams.microsoft.com. Try: az login --scope https://dev.teams.microsoft.com/.default"
}

$url = "https://dev.teams.microsoft.com/api/v1.0/agentblueprints/$blueprintId/backendConfiguration"

# Bot ID is the same as the agent blueprint ID (see readme Step 4).
$body = @{
    type     = "botBased"
    botBased = @{
        botId = $blueprintId
    }
} | ConvertTo-Json -Depth 5

Write-Host "PUT $url"
Write-Host "Body:"
Write-Host $body

try {
    $response = Invoke-RestMethod -Uri $url `
        -Method Put `
        -Headers @{
            "Content-Type"  = "application/json"
            "Accept"        = "application/json"
            "Authorization" = "Bearer $token"
        } `
        -Body $body

    Write-Host ""
    Write-Host "Response:"
    if ($response) {
        $response | ConvertTo-Json -Depth 5 | Write-Host
    } else {
        Write-Host "(empty response)"
    }
}
catch {
    Write-Host "Failed to configure blueprint backend: $($_.Exception.Message)"
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
        Write-Host "Error details: $($_.ErrorDetails.Message)"
    }
    throw
}

Write-Host "Blueprint backend configuration completed for blueprint $blueprintId."
