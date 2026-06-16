<#
.SYNOPSIS
    Pre-flight region check for the byom-cross-region extension on top of
    template 16 (private-network standard agent + APIM). Picks a region pair
    (or asks the user), verifies the deployment will actually succeed there,
    and — if it won't — ranks alternative regions where it will.

.DESCRIPTION
    The extension's main.bicep provisions resources across two regions:

      Project region  Foundry project + APIM AI Gateway (StandardV2)
                      + at least one local model deployment
                      (defaults to gpt-4o GlobalStandard, override with
                      -ProjectModels / -ModelSku)

      Backend region  Backend Foundry account hosting the models you
                      want to expose through the gateway
                      (defaults to gpt-4o / gpt-5 / gpt-5.1 GlobalStandard,
                      override with -BackendModels / -ModelSku)

    For each region the script verifies, in this order:

      0.  (subscription-wide, once) All required ARM providers are
          'Registered' on the subscription
      1.  Region is in the template-16 main.bicep @allowed list
          (project region only — backend account has no @allowed list)
      2.  CognitiveServices AIServices kind is regionally available
      3.  APIM service provider supports the region AND the StandardV2
          SKU is supported there (project region only)
      4.  AI Search, Storage and Cosmos DB are regionally available
          (project region only — template 16 co-locates them with the project)
      5.  Each requested (model, version, SKU) tuple is in the published
          model catalogue for that region
      6.  The OpenAI quota family for each model has enough free capacity
          (limit - currentValue >= required)

    Customer warnings (non-blocking) are surfaced for:
      * Cosmos canary regions (eastus2euap, centraluseuap) — bicep rewrites
        Cosmos to westus
      * Storage no-ZRS regions (southindia, westus, canadaeast) — bicep
        falls back to Standard_GRS instead of Standard_ZRS

    If any of those fail in the chosen region, the script automatically
    scans the rest of Azure (only the regions where CognitiveServices is
    even supported) and prints the top candidates ranked by free capacity.

    The output of a successful run is the exact `az deployment group create`
    invocation you should use to start the real deployment.

.PARAMETER Subscription
    Subscription id to check. The script switches `az` context to this sub.

.PARAMETER ProjectRegion
    Region for the Foundry project + APIM. Special values:
       prompt   ask interactively (default)
       auto     pick the best available region automatically

.PARAMETER BackendRegion
    Region for the backend Foundry account + models. Same special values.

.PARAMETER ProjectModels
    Models that must be available + have quota in the project region. Each
    entry is either "<name>" (any version with -ModelSku) or "<name>@<version>"
    (exact version). Defaults to the project-side model in main.bicepparam.

.PARAMETER BackendModels
    Models that must be available + have quota in the backend region. Same
    format as -ProjectModels. Defaults to the backendModelDeployments list
    in main.bicepparam.

.PARAMETER ModelSku
    Cognitive Services SKU for every model in -ProjectModels / -BackendModels.
    Default 'GlobalStandard' (matches projectModelSkuName + every
    backendModelDeployments[*].skuName in main.bicepparam).

.PARAMETER ProjectModelCapacity
    TPM (in thousands) required for EACH model in -ProjectModels.
    Default 30 (matches projectModelCapacity in main.bicepparam).

.PARAMETER BackendModelCapacity
    TPM (in thousands) required for EACH model in -BackendModels.
    Default 10 (matches backendModelDeployments[*].capacity in main.bicepparam).

.PARAMETER MaxAlternatives
    How many alternative regions to suggest when the chosen region fails.
    Default 5.

.PARAMETER NoSuggest
    Skip the alternatives scan when the chosen region fails.

.PARAMETER ListAvailable
    Skip the chosen-region check and just enumerate every region where the
    full deployment would succeed today.

.PARAMETER BringYourOwnSearch
    Skip the AI Search region check (the deployment will reuse an existing
    Search service passed via aiSearchResourceId in main.bicepparam).

.PARAMETER BringYourOwnStorage
    Skip the Storage region check (the deployment will reuse an existing
    storage account passed via azureStorageAccountResourceId).

.PARAMETER BringYourOwnCosmos
    Skip the Cosmos DB region check (the deployment will reuse an existing
    account passed via azureCosmosDBAccountResourceId).

.EXAMPLE
    .\check-region.ps1 -Subscription <sub-id>
    Fully interactive: prompts for project and backend regions.

.EXAMPLE
    .\check-region.ps1 -Subscription <sub-id> -ProjectRegion canadaeast -BackendRegion japaneast
    Non-interactive: reports PASS/FAIL, suggests alternatives if FAIL.

.EXAMPLE
    .\check-region.ps1 -Subscription <sub-id> -ProjectRegion auto -BackendRegion auto
    Auto-selects the best regions on both sides and prints the deploy command.

.EXAMPLE
    .\check-region.ps1 -Subscription <sub-id> `
      -ProjectRegion eastus -BackendRegion swedencentral `
      -BackendModels gpt-4.1,gpt-4o-mini@2024-07-18 `
      -BackendModelCapacity 50
    Customize the backend model list and per-model capacity.

.EXAMPLE
    .\check-region.ps1 -Subscription <sub-id> -ListAvailable
    Surveys every CognitiveServices region and lists which ones would work.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]   $Subscription,
    [string]   $ProjectRegion         = "prompt",
    [string]   $BackendRegion         = "prompt",
    [string[]] $ProjectModels         = @('gpt-4o@2024-11-20'),
    [string[]] $BackendModels         = @('gpt-4o@2024-11-20','gpt-5@2025-08-07','gpt-5.1@2025-11-13'),
    [string]   $ModelSku              = "GlobalStandard",
    [int]      $ProjectModelCapacity  = 30,
    [int]      $BackendModelCapacity  = 10,
    [int]      $MaxAlternatives       = 5,
    [switch]   $NoSuggest,
    [switch]   $ListAvailable,
    [switch]   $BringYourOwnSearch,
    [switch]   $BringYourOwnStorage,
    [switch]   $BringYourOwnCosmos
)

$ErrorActionPreference = "Stop"

# -------------------------------------------------------------------
# Hardcoded region lists for things ARM does not expose cleanly
# -------------------------------------------------------------------

# The `@allowed` list at the top of
#   scripts/00-foundry-project/template/main.bicep
# If the project region isn't here, the bicep won't even compile.
# Keep in lock-step with the template.
$script:TemplateAllowedRegions = @(
    'westus','eastus','eastus2','japaneast','francecentral','spaincentral',
    'uaenorth','southcentralus','italynorth','germanywestcentral',
    'brazilsouth','southafricanorth','australiaeast','swedencentral',
    'canadaeast','westeurope','westus3','uksouth','southindia',
    'koreacentral','polandcentral','switzerlandnorth','norwayeast'
)

# APIM StandardV2 SKU regional availability. The public ARM provider list
# returns every region where ANY APIM SKU is supported, NOT specifically
# StandardV2. Source of truth: https://aka.ms/apim-v2-tiers (Microsoft Learn
# "Available API Management v2 tiers and regions"). Update when MS expands
# the list — last refreshed June 2026.
$script:ApimStandardV2Regions = @(
    'australiacentral','australiaeast','australiasoutheast',
    'brazilsouth','canadacentral','canadaeast',
    'centralindia','centralus','eastasia','eastus','eastus2',
    'francecentral','germanywestcentral',
    'italynorth','japaneast','koreacentral','northcentralus','northeurope',
    'norwayeast','polandcentral','southafricanorth','southcentralus',
    'southeastasia','southindia','spaincentral','swedencentral',
    'switzerlandnorth','uaenorth','uksouth','ukwest',
    'westcentralus','westeurope','westus','westus2','westus3'
)

# Bicep behaviour patches we inherit from
# scripts/00-foundry-project/template/modules-network-secured/standard-dependent-resources.bicep
$script:CosmosCanaryRegions     = @('eastus2euap','centraluseuap')   # bicep rewrites Cosmos to westus
$script:StorageNoZrsRegions     = @('southindia','westus','canadaeast') # bicep falls back to Standard_GRS

# Resource providers the deployment will touch. Anything NotRegistered will
# block the very first ARM call.
$script:RequiredProviders = @(
    'Microsoft.CognitiveServices',
    'Microsoft.ApiManagement',
    'Microsoft.Search',
    'Microsoft.Storage',
    'Microsoft.DocumentDB',
    'Microsoft.Network',
    'Microsoft.ManagedIdentity',
    'Microsoft.Authorization',
    'Microsoft.Resources',
    'Microsoft.ContainerRegistry',
    'Microsoft.App'
)

# -------------------------------------------------------------------
# Requirements: what each region "role" needs to support
# -------------------------------------------------------------------

# Parse a "name" or "name@version" string into a model requirement object.
# Version is optional; when omitted, the script accepts any version of the
# model with the given SKU.
function Convert-ModelSpec {
    param(
        [Parameter(Mandatory)] [string]$Spec,
        [Parameter(Mandatory)] [string]$Sku,
        [Parameter(Mandatory)] [int]   $Capacity
    )
    $trimmed = $Spec.Trim()
    if ($trimmed -match '^(?<name>[^@]+)@(?<version>.+)$') {
        $name    = $Matches['name'].Trim()
        $version = $Matches['version'].Trim()
    } else {
        $name    = $trimmed
        $version = '*'
    }
    if ([string]::IsNullOrWhiteSpace($name)) {
        throw "Invalid model spec '$Spec' — expected '<name>' or '<name>@<version>'."
    }
    return [pscustomobject]@{
        Name     = $name
        Version  = $version
        Sku      = $Sku
        Capacity = $Capacity
    }
}

$ProjectRequirements = [pscustomobject]@{
    Role        = "Project (Foundry + APIM)"
    NeedsApim   = $true
    NeedsTemplateAllowed = $true
    NeedsDependencies    = $true   # Search + Storage + Cosmos in same region
    NeedsHostedAgent     = $false  # extension does not provision hosted agent
    Models      = @($ProjectModels | ForEach-Object {
        Convert-ModelSpec -Spec $_ -Sku $ModelSku -Capacity $ProjectModelCapacity
    })
}

$BackendRequirements = [pscustomobject]@{
    Role        = "Backend (model host)"
    NeedsApim   = $false
    NeedsTemplateAllowed = $false  # backend account has no @allowed list
    NeedsDependencies    = $false
    NeedsHostedAgent     = $false
    Models      = @($BackendModels | ForEach-Object {
        Convert-ModelSpec -Spec $_ -Sku $ModelSku -Capacity $BackendModelCapacity
    })
}

# -------------------------------------------------------------------
# Output helpers
# -------------------------------------------------------------------

function Write-Status {
    param(
        [ValidateSet('PASS','WARN','FAIL','INFO')] [string]$Status,
        [string]$Message
    )
    $color = switch ($Status) {
        'PASS' { 'Green' }; 'WARN' { 'Yellow' }; 'FAIL' { 'Red' }; 'INFO' { 'Cyan' }
    }
    $tag = switch ($Status) {
        'PASS' { '[ OK ]' }; 'WARN' { '[WARN]' }; 'FAIL' { '[FAIL]' }; 'INFO' { '[INFO]' }
    }
    Write-Host "    $tag  $Message" -ForegroundColor $color
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "--- $Title $('-' * [Math]::Max(0, 65 - $Title.Length))" -ForegroundColor White
}

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "=================================================================" -ForegroundColor Cyan
}

# -------------------------------------------------------------------
# Azure REST helpers (PowerShell 5.1 compatible)
# -------------------------------------------------------------------

function Invoke-AzJson {
    # Wraps "az ... -o json" + ConvertFrom-Json with quiet failure semantics.
    # Returns $null on failure (so callers can treat "no data" as a degraded
    # check rather than crashing the whole script).
    param([Parameter(Mandatory)][string[]]$AzArgs)
    try {
        $raw = & az @AzArgs --only-show-errors -o json 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $raw) { return $null }
        $text = if ($raw -is [array]) { $raw -join "`n" } else { "$raw" }
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text | ConvertFrom-Json
    } catch { return $null }
}

function Invoke-AzRestJson {
    # az rest wrapper that takes a hashtable body and ALWAYS passes it via
    # --body $variable (not @file) — files get tripped up by PowerShell @
    # splat semantics and by CR/LF mangling.
    param(
        [Parameter(Mandatory)][string]$Url,
        [string]$Method = 'GET',
        $Body
    )
    $argv = @('rest','--method', $Method.ToLower(), '--url', $Url, '--only-show-errors')
    if ($null -ne $Body) {
        $bodyJson = if ($Body -is [string]) { $Body } else { ($Body | ConvertTo-Json -Compress -Depth 10) }
        $argv += @('--body', $bodyJson, '--headers', 'Content-Type=application/json')
    }
    try {
        $raw = & az @argv 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $raw) { return $null }
        $text = if ($raw -is [array]) { $raw -join "`n" } else { "$raw" }
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text | ConvertFrom-Json
    } catch { return $null }
}

# -------------------------------------------------------------------
# Provider locations + caches
# -------------------------------------------------------------------

$script:Cache_ProviderLocs = @{}
$script:Cache_ModelList    = @{}
$script:Cache_Usage        = @{}

function Get-ProviderLocations {
    param([string]$Namespace, [string]$ResourceType)
    $key = "$Namespace/$ResourceType"
    if ($script:Cache_ProviderLocs.ContainsKey($key)) {
        return $script:Cache_ProviderLocs[$key]
    }
    # az provider show negotiates the right Microsoft.Resources API version
    # itself, which avoids the InvalidApiVersionParameter trap.
    $r = Invoke-AzJson -AzArgs @('provider','show','--namespace', $Namespace)
    $locs = @()
    if ($r -and $r.resourceTypes) {
        $rt = $r.resourceTypes | Where-Object { $_.resourceType -eq $ResourceType } | Select-Object -First 1
        if ($rt -and $rt.locations) {
            $locs = @($rt.locations | ForEach-Object { ($_ -replace ' ','').ToLower() })
        }
    }
    $script:Cache_ProviderLocs[$key] = $locs
    return $locs
}

function Get-RegionModelList {
    param([string]$Region)
    $key = $Region.ToLower()
    if ($script:Cache_ModelList.ContainsKey($key)) {
        return $script:Cache_ModelList[$key]
    }
    $models = Invoke-AzJson -AzArgs @('cognitiveservices','model','list','--location', $Region)
    if ($null -eq $models) { $models = @() }
    $script:Cache_ModelList[$key] = $models
    return $models
}

function Get-RegionUsage {
    param([string]$Region)
    $key = $Region.ToLower()
    if ($script:Cache_Usage.ContainsKey($key)) {
        return $script:Cache_Usage[$key]
    }
    $usage = Invoke-AzJson -AzArgs @('cognitiveservices','usage','list','--location', $Region)
    if ($null -eq $usage) { $usage = @() }
    $script:Cache_Usage[$key] = $usage
    return $usage
}

# -------------------------------------------------------------------
# Check primitives
# -------------------------------------------------------------------

function Test-ModelAvailable {
    param([array]$Inventory, [string]$Name, [string]$Version, [string]$Sku)
    if (-not $Inventory -or $Inventory.Count -eq 0) { return $false }
    # Each entry in `az cognitiveservices model list` is a (kind, model)
    # tuple; deployment SKUs (GlobalStandard, ProvisionedManaged, etc.)
    # live in `model.skus[].name`. Accept either `OpenAI` or `AIServices`
    # kinds — Foundry projects use AIServices, classic AOAI uses OpenAI.
    # Version '*' means "any version is fine".
    return @($Inventory | Where-Object {
        ($_.kind -eq 'OpenAI' -or $_.kind -eq 'AIServices') -and
        $_.model.name -eq $Name -and
        ($Version -eq '*' -or $_.model.version -eq $Version) -and
        @($_.model.skus | Where-Object { $_.name -eq $Sku }).Count -gt 0
    }).Count -gt 0
}

function Get-AvailableVersions {
    param([array]$Inventory, [string]$Name, [string]$Sku)
    if (-not $Inventory -or $Inventory.Count -eq 0) { return @() }
    return @($Inventory | Where-Object {
        ($_.kind -eq 'OpenAI' -or $_.kind -eq 'AIServices') -and
        $_.model.name -eq $Name -and
        @($_.model.skus | Where-Object { $_.name -eq $Sku }).Count -gt 0
    } | ForEach-Object { $_.model.version } | Sort-Object -Unique)
}

function Find-QuotaRow {
    # Quota rows are model-family based, not version-pinned. The exact
    # name format is `OpenAI.{Sku}.{ModelFamily}` but we accept dotted
    # variants for new families like gpt-5.1.
    param([array]$Usage, [string]$ModelName, [string]$Sku)
    if (-not $Usage -or $Usage.Count -eq 0) { return $null }
    $candidates = @(
        "OpenAI.$Sku.$ModelName",
        "OpenAI.$Sku.$ModelName-*"
    )
    foreach ($pat in $candidates) {
        $row = $Usage | Where-Object { $_.name.value -like $pat } | Select-Object -First 1
        if ($row) { return $row }
    }
    # Final loose match: any row containing both the SKU and the model name.
    return ($Usage | Where-Object {
        $_.name.value -like "*$Sku*$ModelName*"
    } | Select-Object -First 1)
}

# Build a structured check result for one (region, requirements) pair.
function Test-Region {
    param(
        [string]$Region,
        [pscustomobject]$Requirements,
        [bool]$Quiet = $false
    )

    $result = [pscustomobject]@{
        Region        = $Region
        Role          = $Requirements.Role
        Status        = 'PASS'
        Checks        = @()
        TotalFreeTpm  = 0
        Missing       = @()
        Warnings      = @()
    }

    function Add-Check([string]$Name, [string]$Status, [string]$Message) {
        $result.Checks += [pscustomobject]@{ Name = $Name; Status = $Status; Message = $Message }
        if ($Status -eq 'FAIL') { $result.Status = 'FAIL' }
        if ($Status -eq 'WARN') { $result.Warnings += "$Name — $Message" }
        if (-not $Quiet) { Write-Status -Status $Status -Message ("{0,-50} {1}" -f $Name, $Message) }
    }

    # 0. Template @allowed list (project region only — backend uses a
    # separate, parameter-light remote-account.bicep that has no allowed list).
    if ($Requirements.NeedsTemplateAllowed) {
        if ($script:TemplateAllowedRegions -notcontains $Region.ToLower()) {
            Add-Check "Region in template @allowed list" 'FAIL' "Not in main.bicep @allowed (would fail bicep compile)"
            $result.Missing += "template @allowed list"
            return $result
        } else {
            Add-Check "Region in template @allowed list" 'PASS' "Accepted by main.bicep"
        }
    }

    # 1. CognitiveServices AIServices region support
    $cogLocs = Get-ProviderLocations -Namespace 'Microsoft.CognitiveServices' -ResourceType 'accounts'
    if ($cogLocs -notcontains $Region.ToLower()) {
        Add-Check "CognitiveServices/accounts in $Region" 'FAIL' "Provider does not list this region"
        $result.Missing += "CognitiveServices region support"
        return $result
    } else {
        Add-Check "CognitiveServices/accounts in $Region" 'PASS' "Region listed by ARM provider"
    }

    # 2. APIM service + StandardV2 SKU (project only)
    if ($Requirements.NeedsApim) {
        $apimLocs = Get-ProviderLocations -Namespace 'Microsoft.ApiManagement' -ResourceType 'service'
        if ($apimLocs -notcontains $Region.ToLower()) {
            Add-Check "ApiManagement/service in $Region" 'FAIL' "Provider does not list this region"
            $result.Missing += "APIM region support"
        } else {
            Add-Check "ApiManagement/service in $Region" 'PASS' "Region listed by ARM provider"
        }
        # StandardV2 SKU is more selective than the general APIM provider list.
        if ($script:ApimStandardV2Regions -notcontains $Region.ToLower()) {
            Add-Check "APIM StandardV2 SKU in $Region" 'FAIL' "Not in current SV2 region list (see aka.ms/apim-v2-tiers)"
            $result.Missing += "APIM StandardV2 SKU"
        } else {
            Add-Check "APIM StandardV2 SKU in $Region" 'PASS' "SV2 supported"
        }
    }

    # 2b. Foundry dependencies — AI Search, Storage, Cosmos DB (project only)
    if ($Requirements.NeedsDependencies) {

        # AI Search
        if ($script:BringYourOwnSearch_Flag) {
            Add-Check "Search/searchServices in $Region" 'INFO' "Skipped (BringYourOwnSearch)"
        } else {
            $searchLocs = Get-ProviderLocations -Namespace 'Microsoft.Search' -ResourceType 'searchServices'
            if ($searchLocs -notcontains $Region.ToLower()) {
                Add-Check "Search/searchServices in $Region" 'FAIL' "Provider does not list this region"
                $result.Missing += "AI Search region support"
            } else {
                Add-Check "Search/searchServices in $Region" 'PASS' "Region listed by ARM provider (standard SKU)"
            }
        }

        # Storage (region availability is essentially universal, but check
        # for completeness AND surface the ZRS-fallback patch from the bicep)
        if ($script:BringYourOwnStorage_Flag) {
            Add-Check "Storage/storageAccounts in $Region" 'INFO' "Skipped (BringYourOwnStorage)"
        } else {
            $storageLocs = Get-ProviderLocations -Namespace 'Microsoft.Storage' -ResourceType 'storageAccounts'
            if ($storageLocs -notcontains $Region.ToLower()) {
                Add-Check "Storage/storageAccounts in $Region" 'FAIL' "Provider does not list this region"
                $result.Missing += "Storage region support"
            } elseif ($script:StorageNoZrsRegions -contains $Region.ToLower()) {
                Add-Check "Storage/storageAccounts in $Region" 'WARN' "Region has no ZRS — bicep will deploy Standard_GRS instead"
            } else {
                Add-Check "Storage/storageAccounts in $Region" 'PASS' "Standard_ZRS supported"
            }
        }

        # Cosmos DB (DocumentDB)
        if ($script:BringYourOwnCosmos_Flag) {
            Add-Check "DocumentDB/databaseAccounts in $Region" 'INFO' "Skipped (BringYourOwnCosmos)"
        } else {
            $cosmosLocs = Get-ProviderLocations -Namespace 'Microsoft.DocumentDB' -ResourceType 'databaseAccounts'
            if ($script:CosmosCanaryRegions -contains $Region.ToLower()) {
                Add-Check "DocumentDB/databaseAccounts in $Region" 'WARN' "Canary region — bicep will deploy Cosmos to westus instead"
            } elseif ($cosmosLocs -notcontains $Region.ToLower()) {
                Add-Check "DocumentDB/databaseAccounts in $Region" 'FAIL' "Provider does not list this region"
                $result.Missing += "Cosmos DB region support"
            } else {
                Add-Check "DocumentDB/databaseAccounts in $Region" 'PASS' "Region listed by ARM provider"
            }
        }
    }

    # 3 & 4. Model availability + quota
    $inventory = Get-RegionModelList -Region $Region
    $usage     = Get-RegionUsage     -Region $Region

    foreach ($m in $Requirements.Models) {
        $verLabel = if ($m.Version -eq '*') { ' (any version)' } else { "@$($m.Version)" }
        $label = "$($m.Name)$verLabel [$($m.Sku)]"

        # 3. Catalogue
        if (Test-ModelAvailable -Inventory $inventory -Name $m.Name -Version $m.Version -Sku $m.Sku) {
            if ($m.Version -eq '*') {
                $available = Get-AvailableVersions -Inventory $inventory -Name $m.Name -Sku $m.Sku
                Add-Check $label 'PASS' ("Available in catalogue (versions: " + ($available -join ', ') + ")")
            } else {
                Add-Check $label 'PASS' "Available in catalogue"
            }
        } else {
            $alt = Get-AvailableVersions -Inventory $inventory -Name $m.Name -Sku $m.Sku
            if ($alt.Count -gt 0) {
                Add-Check $label 'FAIL' ("Version $($m.Version) not available; have: " + ($alt -join ', '))
            } else {
                Add-Check $label 'FAIL' "$($m.Name) not available at $($m.Sku) SKU in this region"
            }
            $result.Missing += $label
            continue
        }

        # 4. Quota
        $row = Find-QuotaRow -Usage $usage -ModelName $m.Name -Sku $m.Sku
        if (-not $row) {
            Add-Check "  quota: $($m.Name)" 'WARN' "No quota row found yet (limit not visible — may auto-provision at first deploy)"
        } else {
            $free = [int]$row.limit - [int]$row.currentValue
            $result.TotalFreeTpm += $free
            $label2 = "  quota: $($row.name.value)"
            if ($free -ge $m.Capacity) {
                Add-Check $label2 'PASS' "free $free / limit $([int]$row.limit) >= requested $($m.Capacity) (k TPM)"
            } else {
                Add-Check $label2 'FAIL' "free $free / limit $([int]$row.limit) < requested $($m.Capacity) — request a quota increase"
                $result.Missing += "$($m.Name) quota"
            }
        }
    }

    # 5. Phase 6 — ACR (Premium) + Container Apps env (project only)
    if ($Requirements.NeedsHostedAgent -and -not $script:SkipHostedAgent_Flag) {

        # Microsoft.ContainerRegistry/registries — Premium SKU follows the
        # general provider region list (PE support is universal across regions
        # where ACR is offered, which is all of them).
        if ($script:BringYourOwnAcr_Flag) {
            Add-Check "ContainerRegistry/registries in $Region" 'INFO' "Skipped (BringYourOwnAcr)"
        } else {
            $acrLocs = Get-ProviderLocations -Namespace 'Microsoft.ContainerRegistry' -ResourceType 'registries'
            if ($acrLocs -notcontains $Region.ToLower()) {
                Add-Check "ContainerRegistry/registries in $Region" 'FAIL' "Provider does not list this region"
                $result.Missing += "ACR region support"
            } else {
                Add-Check "ContainerRegistry/registries in $Region" 'PASS' "Premium SKU supported"
            }
        }

        # Microsoft.App/managedEnvironments — needed for the workload-profiles
        # Container Apps environment that hosts the agent
        $acaLocs = Get-ProviderLocations -Namespace 'Microsoft.App' -ResourceType 'managedEnvironments'
        if ($acaLocs -notcontains $Region.ToLower()) {
            Add-Check "App/managedEnvironments in $Region" 'FAIL' "Provider does not list this region"
            $result.Missing += "Container Apps region support"
        } else {
            Add-Check "App/managedEnvironments in $Region" 'PASS' "Workload-profiles env supported"
        }
    }

    return $result
}

# -------------------------------------------------------------------
# Alternative-region ranking
# -------------------------------------------------------------------

function Find-CandidateRegions {
    param([pscustomobject]$Requirements, [string]$Exclude = "")

    # The set of all regions where CognitiveServices is supported on this sub.
    $cogLocs = Get-ProviderLocations -Namespace 'Microsoft.CognitiveServices' -ResourceType 'accounts'
    if ($Requirements.NeedsApim) {
        $apimLocs = Get-ProviderLocations -Namespace 'Microsoft.ApiManagement' -ResourceType 'service'
        $cogLocs  = @($cogLocs | Where-Object { $apimLocs -contains $_ })
        # APIM StandardV2 SKU is more selective.
        $cogLocs  = @($cogLocs | Where-Object { $script:ApimStandardV2Regions -contains $_ })
    }
    if ($Requirements.NeedsTemplateAllowed) {
        $cogLocs = @($cogLocs | Where-Object { $script:TemplateAllowedRegions -contains $_ })
    }
    if ($Requirements.NeedsDependencies) {
        # Foundry dependencies must live in the same region (template wires
        # them up there). BYO flags let the customer skip these gates.
        if (-not $script:BringYourOwnSearch_Flag) {
            $searchLocs  = Get-ProviderLocations -Namespace 'Microsoft.Search'     -ResourceType 'searchServices'
            $cogLocs     = @($cogLocs | Where-Object { $searchLocs -contains $_ })
        }
        if (-not $script:BringYourOwnStorage_Flag) {
            $storageLocs = Get-ProviderLocations -Namespace 'Microsoft.Storage'    -ResourceType 'storageAccounts'
            $cogLocs     = @($cogLocs | Where-Object { $storageLocs -contains $_ })
        }
        if (-not $script:BringYourOwnCosmos_Flag) {
            $cosmosLocs  = Get-ProviderLocations -Namespace 'Microsoft.DocumentDB' -ResourceType 'databaseAccounts'
            # Allow canary regions (they self-rewrite to westus inside the bicep).
            $cogLocs     = @($cogLocs | Where-Object {
                ($cosmosLocs -contains $_) -or ($script:CosmosCanaryRegions -contains $_)
            })
        }
    }
    if ($Requirements.NeedsHostedAgent -and -not $script:SkipHostedAgent_Flag) {
        if (-not $script:BringYourOwnAcr_Flag) {
            $acrLocs = Get-ProviderLocations -Namespace 'Microsoft.ContainerRegistry' -ResourceType 'registries'
            $cogLocs = @($cogLocs | Where-Object { $acrLocs -contains $_ })
        }
        $acaLocs = Get-ProviderLocations -Namespace 'Microsoft.App' -ResourceType 'managedEnvironments'
        $cogLocs = @($cogLocs | Where-Object { $acaLocs -contains $_ })
    }

    $excludeKey = $Exclude.ToLower()
    $candidates = $cogLocs | Where-Object { $_ -ne $excludeKey }

    Write-Host ""
    Write-Host "  Scanning $($candidates.Count) candidate region$(if ($candidates.Count -ne 1) {'s'}) for $($Requirements.Role)..." -ForegroundColor Cyan
    Write-Host "  (this fetches model + quota lists per region; ~1-2 s each)" -ForegroundColor DarkGray

    $results = @()
    $i = 0
    foreach ($r in $candidates) {
        $i++
        Write-Host -NoNewline "    [$i/$($candidates.Count)] $r ... " -ForegroundColor DarkGray
        $res = Test-Region -Region $r -Requirements $Requirements -Quiet $true
        if ($res.Status -eq 'PASS') {
            Write-Host "OK (free=$($res.TotalFreeTpm))" -ForegroundColor Green
        } else {
            Write-Host "skip ($($res.Missing -join '; '))" -ForegroundColor DarkGray
        }
        $results += $res
    }

    return $results
}

# -------------------------------------------------------------------
# Subscription-wide pre-check: resource provider registration
# -------------------------------------------------------------------

function Test-ProvidersRegistered {
    # Returns @{ Ok = bool; NotRegistered = string[] }
    Write-Section "Resource provider registration (subscription-wide)"
    $allProviders = Invoke-AzJson -AzArgs @('provider','list','--query',"[].{namespace:namespace,state:registrationState}")
    if (-not $allProviders) {
        Write-Status -Status 'WARN' -Message "Could not list providers — skipping check"
        return @{ Ok = $true; NotRegistered = @() }
    }
    $byNs = @{}
    foreach ($p in $allProviders) { $byNs[$p.namespace] = $p.state }

    $missing = @()
    foreach ($ns in $script:RequiredProviders) {
        $state = $byNs[$ns]
        if (-not $state) {
            Write-Status -Status 'FAIL' -Message ("{0,-50} not visible on subscription" -f $ns)
            $missing += $ns
        } elseif ($state -ne 'Registered') {
            Write-Status -Status 'FAIL' -Message ("{0,-50} state={1}" -f $ns, $state)
            $missing += $ns
        } else {
            Write-Status -Status 'PASS' -Message ("{0,-50} Registered" -f $ns)
        }
    }
    return @{ Ok = ($missing.Count -eq 0); NotRegistered = $missing }
}

# -------------------------------------------------------------------
# Interactive prompts
# -------------------------------------------------------------------

function Resolve-Region {
    param(
        [string]$Value,
        [string]$RoleLabel,
        [string]$RepoDefault
    )
    if ($Value -eq 'prompt') {
        Write-Host ""
        Write-Host "Pick a region for: $RoleLabel" -ForegroundColor Cyan
        Write-Host "  Repo default: $RepoDefault"
        Write-Host "  Type a region (e.g. eastus2), 'auto' to auto-select,"
        Write-Host "  or just press Enter to use the repo default."
        $entry = Read-Host "  Region"
        if ([string]::IsNullOrWhiteSpace($entry)) { return $RepoDefault }
        return $entry.Trim().ToLower()
    }
    return $Value.ToLower()
}

# -------------------------------------------------------------------
# Main
# -------------------------------------------------------------------

Write-Header "Pre-flight region check — Foundry + APIM + cross-region backend"

# Make sure we have a working az session against the right sub.
$account = Invoke-AzJson -AzArgs @('account','show')
if (-not $account) {
    Write-Host "az CLI is not logged in. Run: az login" -ForegroundColor Red
    exit 1
}
if ($account.id -ne $Subscription) {
    Write-Host "Switching subscription to $Subscription ..." -ForegroundColor Yellow
    az account set --subscription $Subscription --only-show-errors | Out-Null
    $account = Invoke-AzJson -AzArgs @('account','show')
}
Write-Host " Subscription : $($account.name) ($($account.id))"
Write-Host " Tenant       : $($account.tenantId)"
Write-Host " Identity     : $($account.user.name) [$($account.user.type)]"

# Expose BYO switches at script scope so Test-Region / Find-CandidateRegions
# can see them without an extra parameter on every function signature.
$script:BringYourOwnSearch_Flag  = [bool]$BringYourOwnSearch
$script:BringYourOwnStorage_Flag = [bool]$BringYourOwnStorage
$script:BringYourOwnCosmos_Flag  = [bool]$BringYourOwnCosmos
$script:BringYourOwnAcr_Flag     = $false
$script:SkipHostedAgent_Flag     = $true

# Subscription-wide pre-check: every required RP must be Registered. If
# any is not, the very first ARM call inside the bicep deployment will fail.
$providerCheck = Test-ProvidersRegistered
if (-not $providerCheck.Ok) {
    Write-Host ""
    Write-Host " One or more required resource providers are NOT registered." -ForegroundColor Red
    Write-Host " Register them with:" -ForegroundColor Yellow
    foreach ($ns in $providerCheck.NotRegistered) {
        Write-Host "   az provider register --namespace $ns --subscription $Subscription" -ForegroundColor White
    }
    Write-Host ""
    Write-Host " Re-run this script after each provider shows 'Registered'." -ForegroundColor Yellow
    exit 1
}

# --------------------- ListAvailable mode ---------------------
if ($ListAvailable) {
    $projectModelLabel = ($ProjectModels -join ', ')
    $backendModelLabel = ($BackendModels -join ', ')

    Write-Section "Project regions (Foundry + APIM + $projectModelLabel @ $ProjectModelCapacity k TPM each)"
    $projectScan = Find-CandidateRegions -Requirements $ProjectRequirements
    $projectOk   = $projectScan | Where-Object Status -eq 'PASS' | Sort-Object -Property TotalFreeTpm -Descending
    Write-Host ""
    if ($projectOk.Count -eq 0) {
        Write-Host "  No regions currently satisfy the project requirements." -ForegroundColor Red
    } else {
        Write-Host "  $($projectOk.Count) region(s) currently satisfy the project requirements:" -ForegroundColor Green
        $projectOk | Select-Object -First $MaxAlternatives | ForEach-Object {
            Write-Host ("    {0,-22}  free TPM (sum)= {1}" -f $_.Region, $_.TotalFreeTpm) -ForegroundColor Green
        }
    }

    Write-Section "Backend regions ($backendModelLabel @ $BackendModelCapacity k TPM each)"
    $backendScan = Find-CandidateRegions -Requirements $BackendRequirements
    $backendOk   = $backendScan | Where-Object Status -eq 'PASS' | Sort-Object -Property TotalFreeTpm -Descending
    Write-Host ""
    if ($backendOk.Count -eq 0) {
        Write-Host "  No regions currently satisfy the backend requirements." -ForegroundColor Red
    } else {
        Write-Host "  $($backendOk.Count) region(s) currently satisfy the backend requirements:" -ForegroundColor Green
        $backendOk | Select-Object -First $MaxAlternatives | ForEach-Object {
            Write-Host ("    {0,-22}  free TPM (sum)= {1}" -f $_.Region, $_.TotalFreeTpm) -ForegroundColor Green
        }
    }
    exit 0
}

# --------------------- Resolve regions ---------------------
$ProjectRegion = Resolve-Region -Value $ProjectRegion -RoleLabel "project (Foundry + APIM)" -RepoDefault "canadaeast"
$BackendRegion = Resolve-Region -Value $BackendRegion -RoleLabel ("backend (" + ($BackendModels -join ', ') + ")") -RepoDefault "japaneast"

# 'auto' means pick the top scorer
if ($ProjectRegion -eq 'auto') {
    Write-Section "Auto-selecting project region"
    $scan  = Find-CandidateRegions -Requirements $ProjectRequirements
    $best  = $scan | Where-Object Status -eq 'PASS' | Sort-Object -Property TotalFreeTpm -Descending | Select-Object -First 1
    if (-not $best) {
        Write-Host ""
        Write-Host "  ERROR: no region currently satisfies the project requirements." -ForegroundColor Red
        exit 2
    }
    $ProjectRegion = $best.Region
    Write-Host ""
    Write-Host "  Selected: $ProjectRegion (free TPM = $($best.TotalFreeTpm))" -ForegroundColor Green
}

if ($BackendRegion -eq 'auto') {
    Write-Section "Auto-selecting backend region"
    $scan  = Find-CandidateRegions -Requirements $BackendRequirements
    $best  = $scan | Where-Object Status -eq 'PASS' | Sort-Object -Property TotalFreeTpm -Descending | Select-Object -First 1
    if (-not $best) {
        Write-Host ""
        Write-Host "  ERROR: no region currently satisfies the backend requirements." -ForegroundColor Red
        exit 2
    }
    $BackendRegion = $best.Region
    Write-Host ""
    Write-Host "  Selected: $BackendRegion (free TPM = $($best.TotalFreeTpm))" -ForegroundColor Green
}

# --------------------- Verify the chosen pair ---------------------
Write-Section "Verifying PROJECT region: $ProjectRegion"
$projectResult = Test-Region -Region $ProjectRegion -Requirements $ProjectRequirements

Write-Section "Verifying BACKEND region: $BackendRegion"
$backendResult = Test-Region -Region $BackendRegion -Requirements $BackendRequirements

# --------------------- Alternatives on failure ---------------------
$projectAlts = @()
$backendAlts = @()

if ($projectResult.Status -ne 'PASS' -and -not $NoSuggest) {
    Write-Section "Project region '$ProjectRegion' won't work — finding alternatives"
    $projectScan = Find-CandidateRegions -Requirements $ProjectRequirements -Exclude $ProjectRegion
    $projectAlts = $projectScan | Where-Object Status -eq 'PASS' | Sort-Object -Property TotalFreeTpm -Descending | Select-Object -First $MaxAlternatives
}

if ($backendResult.Status -ne 'PASS' -and -not $NoSuggest) {
    Write-Section "Backend region '$BackendRegion' won't work — finding alternatives"
    $backendScan = Find-CandidateRegions -Requirements $BackendRequirements -Exclude $BackendRegion
    $backendAlts = $backendScan | Where-Object Status -eq 'PASS' | Sort-Object -Property TotalFreeTpm -Descending | Select-Object -First $MaxAlternatives
}

# --------------------- Summary + recommendation ---------------------
Write-Header "Summary"

function Show-Side {
    param([string]$Label, $Result, $Alternatives)
    $color = if ($Result.Status -eq 'PASS') { 'Green' } else { 'Red' }
    Write-Host (" {0,-20} {1,-15} {2}" -f $Label, $Result.Region, $Result.Status) -ForegroundColor $color
    if ($Result.Status -ne 'PASS') {
        Write-Host "   Missing:" -ForegroundColor Red
        $Result.Missing | Select-Object -Unique | ForEach-Object { Write-Host "     - $_" -ForegroundColor Red }
        if ($Alternatives -and $Alternatives.Count -gt 0) {
            Write-Host "   Try one of these instead (ranked by free TPM):" -ForegroundColor Yellow
            $Alternatives | ForEach-Object {
                Write-Host ("     {0,-22}  free TPM (sum)= {1}" -f $_.Region, $_.TotalFreeTpm) -ForegroundColor Yellow
            }
        } elseif (-not $NoSuggest) {
            Write-Host "   NO alternative region satisfies these requirements today." -ForegroundColor Red
            Write-Host "   Consider lowering the requested capacity or requesting a quota increase." -ForegroundColor Red
        }
    }
}
Show-Side -Label "Project (Foundry+APIM)" -Result $projectResult -Alternatives $projectAlts
Show-Side -Label "Backend (models)"        -Result $backendResult -Alternatives $backendAlts

Write-Host ""
if ($projectResult.Status -eq 'PASS' -and $backendResult.Status -eq 'PASS') {
    Write-Host " Both regions are good. Deploy with:" -ForegroundColor Green
    Write-Host ""
    Write-Host "   cd $(Split-Path -Parent $PSScriptRoot)" -ForegroundColor White
    Write-Host "   az group create --name <rg> --location $ProjectRegion" -ForegroundColor White
    Write-Host "   az deployment group create ``" -ForegroundColor White
    Write-Host "     --resource-group <rg> ``" -ForegroundColor White
    Write-Host "     --template-file main.bicep ``" -ForegroundColor White
    Write-Host "     --parameters '@samples/parameters-cross-region.json' ``" -ForegroundColor White
    Write-Host "     --parameters location=$ProjectRegion backendLocation=$BackendRegion ``" -ForegroundColor White
    Write-Host "                  projectMiClientId=<paste-client-id>" -ForegroundColor White
    Write-Host ""
    Write-Host " (Bash: replace each backtick line-continuation with a backslash.)" -ForegroundColor DarkGray
    Write-Host ""
    exit 0
} else {
    Write-Host " Pre-flight failed. Re-run with one of the suggested regions, e.g.:" -ForegroundColor Red
    $newProj = if ($projectResult.Status -eq 'PASS') { $ProjectRegion } elseif ($projectAlts.Count -gt 0) { $projectAlts[0].Region } else { '<your-choice>' }
    $newBack = if ($backendResult.Status -eq 'PASS') { $BackendRegion } elseif ($backendAlts.Count -gt 0) { $backendAlts[0].Region } else { '<your-choice>' }
    Write-Host ""
    Write-Host "   .\check-region.ps1 -Subscription $Subscription -ProjectRegion $newProj -BackendRegion $newBack" -ForegroundColor White
    Write-Host ""
    exit 1
}
