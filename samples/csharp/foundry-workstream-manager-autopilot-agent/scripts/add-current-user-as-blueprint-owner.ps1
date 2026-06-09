#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

Write-Host "Adding current az login user as owner on the blueprint application..."

$blueprintAppId = $env:AGENT_IDENTITY_BLUEPRINT_ID
if ([string]::IsNullOrWhiteSpace($blueprintAppId)) {
    $resolvedBlueprintId = & azd env get-value AGENT_IDENTITY_BLUEPRINT_ID 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($resolvedBlueprintId)) {
        $blueprintAppId = $resolvedBlueprintId.Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($blueprintAppId)) {
    Write-Warning "AGENT_IDENTITY_BLUEPRINT_ID is not set and could not be resolved from azd env. Skipping blueprint-owner assignment."
    return
}

# This command only works for user-based az logins (not service principal login).
$currentUserId = az ad signed-in-user show --query id -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($currentUserId)) {
    Write-Warning "Could not resolve signed-in user (likely service principal auth). Skipping blueprint-owner assignment."
    return
}

Write-Host "Current user object ID: $currentUserId"

$blueprintAppObjectId = az ad app show --id $blueprintAppId --query id -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($blueprintAppObjectId)) {
    Write-Warning "Could not resolve blueprint application object ID for app ID $blueprintAppId. Skipping blueprint-owner assignment."
    return
}

Write-Host "Blueprint application object ID: $blueprintAppObjectId"

$graphToken = az account get-access-token --resource https://graph.microsoft.com/ --query accessToken -o tsv 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($graphToken)) {
    Write-Warning "Could not acquire a Microsoft Graph token. Skipping blueprint-owner assignment."
    return
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
        Write-Host "Current user is already an owner of the blueprint application; skipping."
        return
    }

    if ($err -and $err.error -and ($err.error.code -eq "Authorization_RequestDenied" -or $err.error.message -like "*Insufficient privileges*")) {
        Write-Warning "Insufficient privileges to add owner on blueprint app ID $blueprintAppId. Ask a tenant admin to add your user as an owner."
        return
    }

    throw
}
