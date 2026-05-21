#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

Write-Host "Adding current az login user as owner on the blueprint application..."

$blueprintAppId = $env:AGENT_IDENTITY_BLUEPRINT_ID
if ([string]::IsNullOrEmpty($blueprintAppId)) {
    throw "AGENT_IDENTITY_BLUEPRINT_ID environment variable is not set."
}

# Get the current signed-in user's object ID (works for user principals; service principals are not supported here).
$currentUserId = az ad signed-in-user show --query id -o tsv
if ([string]::IsNullOrEmpty($currentUserId)) {
    throw "Failed to get the current signed-in user's object ID. Make sure you are logged in via 'az login'."
}

Write-Host "Current user object ID: $currentUserId"

# Resolve the blueprint application's object ID from its App ID.
$blueprintAppObjectId = az ad app show --id $blueprintAppId --query id -o tsv
if ([string]::IsNullOrEmpty($blueprintAppObjectId)) {
    throw "Failed to get application object ID for blueprint app ID $blueprintAppId"
}

Write-Host "Blueprint application object ID: $blueprintAppObjectId"

$graphToken = az account get-access-token --resource https://graph.microsoft.com/ --query accessToken -o tsv
if ([string]::IsNullOrEmpty($graphToken)) {
    throw "Failed to acquire a Microsoft Graph access token."
}

$ownerBody = @{
    "@odata.id" = "https://graph.microsoft.com/v1.0/directoryObjects/$currentUserId"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/applications/$blueprintAppObjectId/owners/`$ref" `
        -Method Post `
        -Headers @{
            "Content-Type"  = "application/json"
            "Accept"        = "application/json"
            "Authorization" = "Bearer $graphToken"
        } `
        -Body $ownerBody

    Write-Host "Current user added as owner of blueprint application $blueprintAppId."
    if ($response) {
        $response | ConvertTo-Json -Depth 5 | Write-Host
    }
}
catch {
    $err = $null
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
        try { $err = $_.ErrorDetails.Message | ConvertFrom-Json } catch { $err = $null }
    }

    if ($err -and $err.error -and $err.error.message -like "*One or more added object references already exist*") {
        Write-Host "Current user is already an owner of the blueprint application; ignoring."
    }
    else {
        throw
    }
}
