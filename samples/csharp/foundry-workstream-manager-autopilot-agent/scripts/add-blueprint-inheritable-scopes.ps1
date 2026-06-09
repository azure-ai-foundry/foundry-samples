<#
.SYNOPSIS
  Adds inheritablePermissions entries to an AgentIdentityBlueprint
  application object via Microsoft Graph beta.

.DESCRIPTION
  Calls
    POST https://graph.microsoft.com/beta/applications/
         microsoft.graph.agentIdentityBlueprint/{objectId}/inheritablePermissions
  and, when an entry for the same resourceAppId already exists, PATCH
    https://graph.microsoft.com/beta/applications/
         microsoft.graph.agentIdentityBlueprint/{objectId}/inheritablePermissions/{resourceAppId}
  under the current `az login`. The signed-in principal must be an owner of
  the blueprint or hold AgentIdentityBlueprint.ReadWrite.All /
  Application.ReadWrite.All.

  Graph allows only one inheritablePermissions entry per resourceAppId.
  This script merges desired scopes into that existing entry (if present)
  so reruns are safe and idempotent.

.PARAMETER BlueprintDisplayName
  Display name (or prefix) of the blueprint app registration. Mutually
  exclusive with -BlueprintAppId / -BlueprintObjectId.

.PARAMETER BlueprintAppId
  AppId (client ID) of the blueprint. Mutually exclusive with the other
  blueprint parameters.

.PARAMETER BlueprintObjectId
  Object ID of the blueprint (what appears in the URL path). Mutually
  exclusive with the other blueprint parameters.

.PARAMETER ResourceAppId
  AppId of the resource whose scopes are being inherited. In direct mode this
  defaults to Microsoft Graph (00000003-0000-0000-c000-000000000000). In
  -FromInstanceAppId mode, this is optional and acts as a filter.

.PARAMETER Scopes
  One or more delegated permission scope names (string values, e.g. "Mail.Read")
  to mark as inheritable from the resource. Required unless -FromInstanceAppId
  is provided.

.PARAMETER FromInstanceAppId
  If provided, copies currently granted delegated scopes from the specified
  AgentAppInstance service principal's oauth2PermissionGrants and adds those
  scopes as inheritable permissions on the blueprint (grouped by resource app).

.EXAMPLE
  ./add-blueprint-inheritable-scopes.ps1 `
      -BlueprintObjectId "5c5a282e-9360-4bec-90cd-e812adff6090" `
      -Scopes "Mail.Read","Mail.Send","Mail.ReadWrite","Chat.ReadWrite","User.ReadBasic.All"

.EXAMPLE
  ./add-blueprint-inheritable-scopes.ps1 `
      -BlueprintDisplayName "digworktt6acct-...-AgentIdentityBlueprint" `
      -Scopes "Mail.Read"

.EXAMPLE
  ./add-blueprint-inheritable-scopes.ps1 `
      -BlueprintAppId "<blueprint-app-id>" `
      -FromInstanceAppId "<per-agent-app-id>"
#>
[CmdletBinding(DefaultParameterSetName = 'ByName')]
param(
    [Parameter(ParameterSetName = 'ByName',     Mandatory)] [string]   $BlueprintDisplayName,
    [Parameter(ParameterSetName = 'ByAppId',    Mandatory)] [string]   $BlueprintAppId,
    [Parameter(ParameterSetName = 'ByObjectId', Mandatory)] [string]   $BlueprintObjectId,

    [string]   $ResourceAppId,
    [string[]] $Scopes,
    [string]   $FromInstanceAppId
)

$ErrorActionPreference = 'Stop'

function Invoke-Graph {
    param([string]$Method, [string]$Uri, [object]$Body)
    $cliArgs = @('rest','--method',$Method,'--uri',$Uri,'--headers','Content-Type=application/json')
    $tmp = $null
    if ($Body) {
        $tmp = New-TemporaryFile
        ($Body | ConvertTo-Json -Depth 20) | Set-Content -Path $tmp -Encoding utf8
        $cliArgs += @('--body',"@$($tmp.FullName)")
    }
    $raw = (az @cliArgs 2>&1 | Out-String)
    if ($tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
    if ($LASTEXITCODE -ne 0) {
        # Treat "already exists" as success (idempotent re-provision)
        if ($raw -match "already exists") {
            Write-Host "  (already exists — skipping)"
        } else {
            throw "Graph $Method $Uri failed`n$raw"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        $trimmed = $raw.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
            try { return ($trimmed | ConvertFrom-Json) } catch { return $trimmed }
        }
    }
}

function Get-InheritableEntriesFromInstance {
    param(
        [Parameter(Mandatory)] [string] $InstanceAppId,
        [string] $FilterResourceAppId
    )

    Write-Host "Resolving instance service principal for appId $InstanceAppId..." -ForegroundColor Cyan
    $instanceSpObjectId = az ad sp show --id $InstanceAppId --query id -o tsv
    if ([string]::IsNullOrWhiteSpace($instanceSpObjectId)) {
        throw "Failed to resolve service principal for instance appId '$InstanceAppId'."
    }
    Write-Host "  instanceSpObjectId=$instanceSpObjectId" -ForegroundColor Green

    $grantFilter = [uri]::EscapeDataString("clientId eq '$instanceSpObjectId' and consentType eq 'AllPrincipals'")
    $grantUri = "https://graph.microsoft.com/v1.0/oauth2PermissionGrants?`$filter=$grantFilter"
    $grants = (Invoke-Graph GET $grantUri $null).value

    if (-not $grants -or @($grants).Count -eq 0) {
        throw "No oauth2PermissionGrants found for instance appId '$InstanceAppId'."
    }

    $byResourceAppId = @{}
    foreach ($grant in @($grants)) {
        if ([string]::IsNullOrWhiteSpace($grant.resourceId) -or [string]::IsNullOrWhiteSpace($grant.scope)) {
            continue
        }

        $resourceSp = Invoke-Graph GET "https://graph.microsoft.com/v1.0/servicePrincipals/$($grant.resourceId)?`$select=id,appId,displayName" $null
        if (-not $resourceSp -or [string]::IsNullOrWhiteSpace($resourceSp.appId)) {
            throw "Failed to resolve resource service principal '$($grant.resourceId)' to appId."
        }

        if (-not [string]::IsNullOrWhiteSpace($FilterResourceAppId) -and $resourceSp.appId -ne $FilterResourceAppId) {
            continue
        }

        if (-not $byResourceAppId.ContainsKey($resourceSp.appId)) {
            $byResourceAppId[$resourceSp.appId] = [ordered]@{
                ResourceAppId      = $resourceSp.appId
                ResourceDisplayName = $resourceSp.displayName
                Scopes             = @()
            }
        }

        $parsedScopes = @($grant.scope -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $byResourceAppId[$resourceSp.appId].Scopes += $parsedScopes
    }

    $entries = @()
    foreach ($resourceAppIdKey in $byResourceAppId.Keys) {
        $entry = $byResourceAppId[$resourceAppIdKey]
        $uniqueScopes = @($entry.Scopes | Select-Object -Unique)
        if ($uniqueScopes.Count -eq 0) {
            continue
        }

        $entries += [pscustomobject]@{
            ResourceAppId       = $entry.ResourceAppId
            ResourceDisplayName = $entry.ResourceDisplayName
            Scopes              = $uniqueScopes
        }
    }

    if ($entries.Count -eq 0) {
        if (-not [string]::IsNullOrWhiteSpace($FilterResourceAppId)) {
            throw "No oauth2PermissionGrant scopes found for instance appId '$InstanceAppId' under resourceAppId '$FilterResourceAppId'."
        }
        throw "No oauth2PermissionGrant scopes found for instance appId '$InstanceAppId'."
    }

    return $entries
}

function Add-InheritablePermissionEntry {
    param(
        [Parameter(Mandatory)] [string] $BlueprintObjectIdValue,
        [Parameter(Mandatory)] [string] $TargetResourceAppId,
        [Parameter(Mandatory)] [string[]] $TargetScopes,
        [string] $ResourceDisplayName
    )

    $desiredScopes = @($TargetScopes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    if ($desiredScopes.Count -eq 0) {
        throw "No scopes provided for resourceAppId $TargetResourceAppId."
    }

    $body = @{
        resourceAppId     = $TargetResourceAppId
        inheritableScopes = @{
            '@odata.type' = 'microsoft.graph.enumeratedScopes'
            scopes        = $desiredScopes
        }
    }

    $collectionUri = "https://graph.microsoft.com/beta/applications/microsoft.graph.agentIdentityBlueprint/$BlueprintObjectIdValue/inheritablePermissions"
    $itemUri = "$collectionUri/$TargetResourceAppId"

    $existing = $null
    try {
        $existing = Invoke-Graph GET $itemUri $null
    }
    catch {
        if ($_.Exception.Message -match "Request_ResourceNotFound|Not Found") {
            $existing = $null
        }
        else {
            throw
        }
    }

    if ($existing) {
        $existingScopes = @()
        if ($existing.inheritableScopes -and $existing.inheritableScopes.scopes) {
            $existingScopes = @($existing.inheritableScopes.scopes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        }

        $mergedScopes = @($existingScopes + $desiredScopes | Select-Object -Unique)
        $existingSorted = (($existingScopes | Sort-Object) -join ' ').Trim()
        $mergedSorted = (($mergedScopes | Sort-Object) -join ' ').Trim()

        if ($existingSorted -eq $mergedSorted) {
            if ([string]::IsNullOrWhiteSpace($ResourceDisplayName)) {
                Write-Host "Existing inheritable scopes already include all requested scopes for resourceAppId=$TargetResourceAppId." -ForegroundColor DarkGray
            }
            else {
                Write-Host "Existing inheritable scopes already include all requested scopes for resourceAppId=$TargetResourceAppId ($ResourceDisplayName)." -ForegroundColor DarkGray
            }
            return
        }

        $patchBody = @{
            inheritableScopes = @{
                '@odata.type' = 'microsoft.graph.enumeratedScopes'
                scopes        = $mergedScopes
            }
        }

        Write-Host ""
        if ([string]::IsNullOrWhiteSpace($ResourceDisplayName)) {
            Write-Host "PATCH $itemUri (resourceAppId=$TargetResourceAppId)" -ForegroundColor Cyan
        }
        else {
            Write-Host "PATCH $itemUri (resourceAppId=$TargetResourceAppId, resource=$ResourceDisplayName)" -ForegroundColor Cyan
        }
        Write-Host "Existing scopes:" -ForegroundColor DarkGray
        Write-Host "  $($existingScopes -join ' ')" -ForegroundColor DarkGray
        Write-Host "Merged scopes:" -ForegroundColor DarkGray
        Write-Host "  $($mergedScopes -join ' ')" -ForegroundColor DarkGray
        Write-Host "Body:" -ForegroundColor DarkGray
        $patchBody | ConvertTo-Json -Depth 10 | Write-Host

        $patchResult = Invoke-Graph PATCH $itemUri $patchBody
        if ($patchResult) {
            Write-Host "Response:" -ForegroundColor DarkGray
            $patchResult | ConvertTo-Json -Depth 20 | Write-Host
        }
        return
    }

    Write-Host ""
    if ([string]::IsNullOrWhiteSpace($ResourceDisplayName)) {
        Write-Host "POST $collectionUri (resourceAppId=$TargetResourceAppId)" -ForegroundColor Cyan
    }
    else {
        Write-Host "POST $collectionUri (resourceAppId=$TargetResourceAppId, resource=$ResourceDisplayName)" -ForegroundColor Cyan
    }
    Write-Host "Body:" -ForegroundColor DarkGray
    $body | ConvertTo-Json -Depth 10 | Write-Host

    $result = Invoke-Graph POST $collectionUri $body

    if ($result) {
        Write-Host "Response:" -ForegroundColor DarkGray
        $result | ConvertTo-Json -Depth 20 | Write-Host
    }
}

if (-not [string]::IsNullOrWhiteSpace($FromInstanceAppId) -and $Scopes -and $Scopes.Count -gt 0) {
    throw "Use either -Scopes (direct mode) OR -FromInstanceAppId (copy mode), not both."
}

if ([string]::IsNullOrWhiteSpace($FromInstanceAppId) -and (-not $Scopes -or $Scopes.Count -eq 0)) {
    throw "Provide -Scopes for direct mode, or -FromInstanceAppId to copy existing instance grants."
}

# 1. Resolve the blueprint object id -------------------------------------
Write-Host "Resolving blueprint..." -ForegroundColor Cyan
switch ($PSCmdlet.ParameterSetName) {
    'ByObjectId' {
        $objectId    = $BlueprintObjectId
        $displayName = $null
        $appId       = $null
    }
    'ByAppId' {
        $found = (Invoke-Graph GET "https://graph.microsoft.com/v1.0/applications?`$filter=appId eq '$BlueprintAppId'&`$select=id,appId,displayName").value
        if (-not $found)           { throw "No app with appId '$BlueprintAppId' found." }
        if (@($found).Count -gt 1) { throw "Multiple apps matched appId '$BlueprintAppId'." }
        $app         = @($found)[0]
        $objectId    = $app.id
        $appId       = $app.appId
        $displayName = $app.displayName
    }
    'ByName' {
        $filter = "startswith(displayName,'$BlueprintDisplayName') or displayName eq '$BlueprintDisplayName'"
        $found  = (Invoke-Graph GET "https://graph.microsoft.com/v1.0/applications?`$filter=$([uri]::EscapeDataString($filter))&`$select=id,appId,displayName").value
        if (-not $found)           { throw "No matching app found." }
        if (@($found).Count -gt 1) { $found | Format-Table displayName,appId,id; throw "Multiple matches -- be more specific or use -BlueprintAppId / -BlueprintObjectId." }
        $app         = @($found)[0]
        $objectId    = $app.id
        $appId       = $app.appId
        $displayName = $app.displayName
    }
}
Write-Host "  objectId=$objectId" -ForegroundColor Green
if ($displayName) { Write-Host "  displayName=$displayName" -ForegroundColor DarkGray }
if ($appId)       { Write-Host "  appId=$appId"             -ForegroundColor DarkGray }

# 2. Build entries ---------------------------------------------------------
$entries = @()
if (-not [string]::IsNullOrWhiteSpace($FromInstanceAppId)) {
    Write-Host ""
    Write-Host "Copy mode: using oauth2PermissionGrants from instance appId $FromInstanceAppId" -ForegroundColor Cyan
    $entries = Get-InheritableEntriesFromInstance -InstanceAppId $FromInstanceAppId -FilterResourceAppId $ResourceAppId
}
else {
    if ([string]::IsNullOrWhiteSpace($ResourceAppId)) {
        $ResourceAppId = '00000003-0000-0000-c000-000000000000'
    }

    $directScopes = @($Scopes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    if ($directScopes.Count -eq 0) {
        throw "Scopes resolved to an empty set."
    }

    $entries = @(
        [pscustomobject]@{
            ResourceAppId       = $ResourceAppId
            ResourceDisplayName = $null
            Scopes              = $directScopes
        }
    )
}

Write-Host ""
Write-Host "Prepared inheritable permission entries:" -ForegroundColor Cyan
foreach ($entry in $entries) {
    $resourceLabel = if ([string]::IsNullOrWhiteSpace($entry.ResourceDisplayName)) { $entry.ResourceAppId } else { "$($entry.ResourceDisplayName) ($($entry.ResourceAppId))" }
    Write-Host "  - $resourceLabel :: $($entry.Scopes -join ' ')" -ForegroundColor DarkGray
}

# 3. POST inheritablePermissions ------------------------------------------
foreach ($entry in $entries) {
    Add-InheritablePermissionEntry `
        -BlueprintObjectIdValue $objectId `
        -TargetResourceAppId $entry.ResourceAppId `
        -TargetScopes $entry.Scopes `
        -ResourceDisplayName $entry.ResourceDisplayName
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
 