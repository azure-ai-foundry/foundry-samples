$ErrorActionPreference = "Stop"

$blueprintSP = az ad sp show --id $env:AGENT_IDENTITY_BLUEPRINT_ID --query id -o tsv

if ([string]::IsNullOrEmpty($blueprintSP)) {
    throw "Failed to get service principal for blueprint ID $($env:AGENT_IDENTITY_BLUEPRINT_ID)"
}

Write-Host "Creating OAuth2 permission grants for blueprint service principal..."


$apxAppId = "5a807f24-c9de-44ee-a3a7-329e88a00ffc"

$apxSP = az ad sp show --id $apxAppId --query id -o tsv
if ([string]::IsNullOrEmpty($apxSP)) {
    throw "Failed to get service principal for APEX app ID $apxAppId"
}

$prodMCPAppId = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1"
$prodMCP_SP = az ad sp show --id $prodMCPAppId --query id -o tsv

if ([string]::IsNullOrEmpty($prodMCP_SP)) {
    throw "Failed to get service principal for Prod MCP app ID $prodMCPAppId"
}

# 00000003-0000-0000-c000-000000000000 is graph appId
$graphAppId = "00000003-0000-0000-c000-000000000000"
$graphSP = az ad sp show --id $graphAppId --query id -o tsv
if ([string]::IsNullOrEmpty($graphSP)) {
    throw "Failed to get service principal for Microsoft Graph app ID $graphAppId"
}

$graphToken = az account get-access-token --resource https://graph.microsoft.com/ --query accessToken -o tsv


$mcpOauthGrant = @"
{
  "clientId": "$blueprintSP",
  "consentType": "AllPrincipals",
  "principalId": null,
  "resourceId": "$prodMCP_SP",
  "scope": "McpServers.M365Admin.All McpServers.DASearch.All McpServers.WebSearch.All McpServers.Files.All AgentTools.MOSEvents.All McpServers.Admin365Graph.All McpServers.ERPAnalytics.All McpServers.DataverseCustom.All McpServers.Dataverse.All McpServers.D365Service.All McpServers.D365Sales.All McpServers.Management.All McpServersMetadata.Read.All McpServers.Developer.All McpServers.CopilotMCP.All McpServers.OneDriveSharepoint.All McpServers.Mail.All McpServers.Teams.All McpServers.Me.All McpServers.Calendar.All McpServers.SharepointLists.All McpServers.Knowledge.All McpServers.Excel.All McpServers.Word.All McpServers.PowerPoint.All"
}
"@
# Catch "Permission entry already exists" error and continue
try {
    $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/oauth2PermissionGrants" `
        -Method Post `
        -Headers @{
            "Content-Type" = "application/json"
            "Accept"       = "application/json"
            "Authorization" = "Bearer $($graphToken)"
        } `
        -Body $mcpOauthGrant

    Write-Host ""
    Write-Host "MCP oauth grant response:"
    $response | ConvertTo-Json -Depth 5 | Write-Host

} catch {
    $err = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($err.error.code -eq "Request_BadRequest" -and
        $err.error.message -like "*Permission entry already exists*") {

        Write-Host "Permission already exists  ignoring."
    }
    else {
        throw
    }
}


try {
    $apxOauthGrant = @"
    {
        "clientId": "$blueprintSP",
        "consentType": "AllPrincipals",
        "principalId": null,
        "resourceId": "$apxSP",
        "scope": "AgentData.ReadWrite"
    }
"@

    $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/oauth2PermissionGrants" `
        -Method Post `
        -Headers @{
            "Content-Type" = "application/json"
            "Accept"       = "application/json"
            "Authorization" = "Bearer $($graphToken)"
        } `
        -Body $apxOauthGrant

    Write-Host ""
    Write-Host "APX oauth grant response:"
    $response | ConvertTo-Json -Depth 5 | Write-Host
}
catch {
    $err = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($err.error.code -eq "Request_BadRequest" -and
        $err.error.message -like "*Permission entry already exists*") {

        Write-Host "Permission already exists  ignoring."
    }
    else {
        throw
    }
}

$graphReactionScopes = @(
    "ChatMessage.Send",
    "ChannelMessage.Send",
    "ChatMember.Read",
    "ChannelMessage.Read.All",
    "User.Read.All"
)
$graphDeprecatedScopes = @(
    "User.Read"
)
$graphReactionScopeString = ($graphReactionScopes -join ' ').Trim()

function Ensure-GraphOauthGrant {
    param(
        [Parameter(Mandatory = $true)][string] $ClientSpObjectId,
        [Parameter(Mandatory = $true)][string] $ClientLabel
    )

    try {
        $graphOauthGrant = @"
        {
            "clientId": "$ClientSpObjectId",
            "consentType": "AllPrincipals",
            "principalId": null,
            "resourceId": "$graphSP",
            "scope": "$graphReactionScopeString"
        }
"@

        $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/oauth2PermissionGrants" `
            -Method Post `
            -Headers @{
                "Content-Type" = "application/json"
                "Accept"       = "application/json"
                "Authorization" = "Bearer $($graphToken)"
            } `
            -Body $graphOauthGrant

        Write-Host ""
        Write-Host "Microsoft Graph oauth grant response ($ClientLabel):"
        $response | ConvertTo-Json -Depth 5 | Write-Host
    }
    catch {
        $errJson = $_.ErrorDetails.Message
        $err = $null
        if (-not [string]::IsNullOrWhiteSpace($errJson)) {
            $err = $errJson | ConvertFrom-Json
        }

        # Graph has returned both "Request_BadRequest" and "Request_MultipleObjectsWithSameKeyValue"
        # for this conflict over time. Match on the message text and accept either code.
        if ($err -and
            ($err.error.code -eq "Request_BadRequest" -or $err.error.code -eq "Request_MultipleObjectsWithSameKeyValue") -and
            $err.error.message -like "*Permission entry already exists*") {

            # oauth2PermissionGrants allows only ONE grant per (clientId, resourceId,
            # consentType, principalId) tuple. To add a new scope to an existing grant,
            # patch the existing record with the merged scope set.
            Write-Host "Permission entry already exists for $ClientLabel - checking whether scope set needs updating."

            $filter = "clientId eq '$ClientSpObjectId' and resourceId eq '$graphSP' and consentType eq 'AllPrincipals'"
            $existingResp = Invoke-RestMethod -Uri ("https://graph.microsoft.com/v1.0/oauth2PermissionGrants?`$filter=" + [uri]::EscapeDataString($filter)) `
                -Method Get `
                -Headers @{
                    "Accept"        = "application/json"
                    "Authorization" = "Bearer $($graphToken)"
                }

            $existing = $existingResp.value | Select-Object -First 1
            if (-not $existing) {
                throw "Graph returned 'Permission entry already exists' for $ClientLabel but the lookup found no matching grant. Aborting."
            }

            $existingScopes = @()
            if (-not [string]::IsNullOrWhiteSpace($existing.scope)) {
                $existingScopes = $existing.scope -split '\s+' | Where-Object { $_ }
            }

            $mergedScopes = @($existingScopes + $graphReactionScopes | Select-Object -Unique)
            $mergedScopes = @($mergedScopes | Where-Object { $graphDeprecatedScopes -notcontains $_ })
            $mergedScopeString = ($mergedScopes -join ' ').Trim()
            $existingScopeStringSorted = (($existingScopes | Sort-Object) -join ' ').Trim()
            $mergedScopeStringSorted = (($mergedScopes | Sort-Object) -join ' ').Trim()

            if ($existingScopeStringSorted -eq $mergedScopeStringSorted) {
                Write-Host "Existing scope set for $ClientLabel already contains all desired scopes; nothing to update."
            }
            else {
                Write-Host "Updating existing grant id=$($existing.id) for ${ClientLabel}:"
                Write-Host "  existing scopes: $($existing.scope)"
                Write-Host "  new scopes:      $mergedScopeString"

                $patchBody = @{ scope = $mergedScopeString } | ConvertTo-Json
                Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/oauth2PermissionGrants/$($existing.id)" `
                    -Method Patch `
                    -Headers @{
                        "Content-Type"  = "application/json"
                        "Accept"        = "application/json"
                        "Authorization" = "Bearer $($graphToken)"
                    } `
                    -Body $patchBody | Out-Null

                Write-Host "Patched Microsoft Graph oauth grant successfully for $ClientLabel."
            }
        }
        else {
            throw
        }
    }
}

Write-Host "Ensuring Microsoft Graph oauth grant on blueprint service principal..."
Ensure-GraphOauthGrant -ClientSpObjectId $blueprintSP -ClientLabel "blueprint SP"

Write-Host "Ensuring blueprint inheritable Microsoft Graph scopes for reactions..."
& "$PSScriptRoot/add-blueprint-inheritable-scopes.ps1" `
    -BlueprintAppId $env:AGENT_IDENTITY_BLUEPRINT_ID `
    -ResourceAppId $graphAppId `
    -Scopes $graphReactionScopes

Write-Host "Per-agent Microsoft Graph oauth grant is intentionally not applied."
Write-Host "This environment relies on blueprint grant + inheritablePermissions."
