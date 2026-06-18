<#
.SYNOPSIS
    Pre-deployment validation for Foundry private network templates.

.DESCRIPTION
    Validates prerequisites before running az deployment group create.
    Catches common misconfigurations that would otherwise surface as
    cryptic ARM errors mid-deploy.

    Checks performed:
    - Resource provider registration
    - Resource group state (existing resources, soft-deleted accounts)
    - BYO resource validation (existence, SKU, configuration)
    - VNet and subnet validation (SALs, delegations, PE capacity)
    - DNS zone conflicts

.PARAMETER ConfigFile
    Path to a config file with key=value pairs. See preflight.config.sample for format.
    When provided, SubscriptionId, ResourceGroup, and Location are read from the file.

.PARAMETER SubscriptionId
    Azure subscription ID (or set in config file)

.PARAMETER ResourceGroup
    Target resource group for deployment (or set in config file)

.PARAMETER Location
    Deployment region. Accepts either display name (e.g. "Sweden Central")
    or API name (e.g. "swedencentral") — the script normalizes automatically.

.PARAMETER ExistingVnetId
    Full ARM resource ID of an existing VNet (optional, for BYO VNet scenarios)

.PARAMETER AiSearchResourceId
    Full ARM resource ID of an existing AI Search resource (optional, for BYO)

.PARAMETER StorageAccountResourceId
    Full ARM resource ID of an existing Storage Account (optional, for BYO)

.PARAMETER CosmosDBResourceId
    Full ARM resource ID of an existing Cosmos DB account (optional, for BYO)

.PARAMETER ApiManagementResourceId
    Full ARM resource ID of an existing API Management instance
    (optional, for APIM private network setup)

.PARAMETER FabricWorkspaceResourceId
    Full ARM resource ID of an existing Fabric Workspace
    (optional, for agent tools / MCP setup)

.PARAMETER ModelName
    Model name for quota checks (e.g., gpt-4o). Leave empty to skip model quota checks.

.PARAMETER ModelFormat
    Model format — must match az cognitiveservices model list output (e.g., OpenAI, Mistral AI)

.PARAMETER ModelSkuName
    Model SKU name (e.g., Standard, GlobalStandard)

.PARAMETER ModelCapacity
    Requested TPM capacity in thousands (e.g., 10 = 10K TPM)

.EXAMPLE
    .\preflight-check.ps1 -ConfigFile .\preflight.config

.EXAMPLE
    .\preflight-check.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg" -Location "swedencentral"

.EXAMPLE
    .\preflight-check.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg" -Location "swedencentral" `
        -ExistingVnetId "/subscriptions/.../virtualNetworks/my-vnet" `
        -AiSearchResourceId "/subscriptions/.../searchServices/my-search"
#>

param(
    [string]$ConfigFile = '',
    [string]$SubscriptionId = '',
    [string]$ResourceGroup = '',
    [string]$Location = '',
    [string]$ExistingVnetId = '',
    [string]$AiSearchResourceId = '',
    [string]$StorageAccountResourceId = '',
    [string]$CosmosDBResourceId = '',
    [string]$ApiManagementResourceId = '',
    [string]$FabricWorkspaceResourceId = '',
    [string]$ModelName = '',
    [string]$ModelFormat = '',
    [string]$ModelSkuName = '',
    [int]$ModelCapacity = 0
)

# --- Load config file if provided ---
if ($ConfigFile) {
    if (-not (Test-Path $ConfigFile)) {
        Write-Host "[FAIL] Config file not found: $ConfigFile" -ForegroundColor Red
        exit 1
    }
    $configLines = Get-Content $ConfigFile | Where-Object { $_ -match '^\s*[^#]' -and $_ -match '=' }
    $config = @{}
    foreach ($line in $configLines) {
        $parts = $line -split '=', 2
        $key = $parts[0].Trim()
        $val = $parts[1].Trim()
        $config[$key] = $val
    }
    # Config values are used as defaults; explicit params override
    if (-not $SubscriptionId -and $config['SubscriptionId']) { $SubscriptionId = $config['SubscriptionId'] }
    if (-not $ResourceGroup -and $config['ResourceGroup']) { $ResourceGroup = $config['ResourceGroup'] }
    if (-not $Location -and $config['Location']) { $Location = $config['Location'] }
    if (-not $ExistingVnetId -and $config['ExistingVnetId']) { $ExistingVnetId = $config['ExistingVnetId'] }
    if (-not $AiSearchResourceId -and $config['AiSearchResourceId']) { $AiSearchResourceId = $config['AiSearchResourceId'] }
    if (-not $StorageAccountResourceId -and $config['StorageAccountResourceId']) { $StorageAccountResourceId = $config['StorageAccountResourceId'] }
    if (-not $CosmosDBResourceId -and $config['CosmosDBResourceId']) { $CosmosDBResourceId = $config['CosmosDBResourceId'] }
    if (-not $ApiManagementResourceId -and $config['ApiManagementResourceId']) { $ApiManagementResourceId = $config['ApiManagementResourceId'] }
    if (-not $FabricWorkspaceResourceId -and $config['FabricWorkspaceResourceId']) { $FabricWorkspaceResourceId = $config['FabricWorkspaceResourceId'] }
    if (-not $ModelName -and $config['ModelName']) { $ModelName = $config['ModelName'] }
    if (-not $ModelFormat -and $config['ModelFormat']) { $ModelFormat = $config['ModelFormat'] }
    if (-not $ModelSkuName -and $config['ModelSkuName']) { $ModelSkuName = $config['ModelSkuName'] }
    if ($ModelCapacity -eq 0 -and $config['ModelCapacity']) { $ModelCapacity = [int]$config['ModelCapacity'] }
}

# --- Validate required params ---
if (-not $SubscriptionId -or -not $ResourceGroup -or -not $Location) {
    Write-Host "ERROR: SubscriptionId, ResourceGroup, and Location are required." -ForegroundColor Red
    Write-Host "Provide them as parameters or in a config file (-ConfigFile)." -ForegroundColor Red
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\preflight-check.ps1 -ConfigFile .\preflight.config"
    Write-Host "  .\preflight-check.ps1 -SubscriptionId 'xxx' -ResourceGroup 'my-rg' -Location 'swedencentral'"
    exit 1
}

# Normalize Location to API format (lowercase, no spaces) so both
# display names like "Sweden Central" and API names like "swedencentral" work.
$originalLocation = $Location
$Location = ($Location -replace '\s','').ToLower()

$ErrorActionPreference = "Continue"
$script:PassCount = 0
$script:FailCount = 0
$script:WarnCount = 0

function Pass  { param([string]$Msg) Write-Host "[PASS] $Msg" -ForegroundColor Green; $script:PassCount++ }
function Fail  { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:FailCount++ }
function Warn  { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow; $script:WarnCount++ }
function Info  { param([string]$Msg) Write-Host "[INFO] $Msg" -ForegroundColor Cyan }

# Helper: validate ARM resource ID format
function Test-ArmResourceId {
    param([string]$Id, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($Id)) { return $false }

    $segments = $Id -split '/'
    # ARM IDs have the form /subscriptions/{guid}/resourceGroups/{rg}/providers/{ns}/{type}/{name}
    if ($segments.Count -lt 9) {
        Fail "$Label resource ID has too few segments ($($segments.Count)). Expected ARM format: /subscriptions/{guid}/resourceGroups/{rg}/providers/{provider}/{type}/{name}"
        return $false
    }
    $subGuid = $segments[2]
    if ($subGuid -notmatch '^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$') {
        Fail "$Label resource ID has invalid subscription GUID: $subGuid"
        return $false
    }
    Pass "$Label resource ID format is valid"
    return $true
}

Write-Host "========================================"
Write-Host "Pre-Deployment Validation"
Write-Host "========================================"
Write-Host "Subscription: $SubscriptionId"
Write-Host "Resource Group: $ResourceGroup"
if ($originalLocation -ne $Location) {
    Write-Host "Location: $Location (normalized from '$originalLocation')"
} else {
    Write-Host "Location: $Location"
}
Write-Host "Existing VNet: $(if ($ExistingVnetId) { $ExistingVnetId } else { '<new>' })"
if ($ApiManagementResourceId) { Write-Host "APIM: $ApiManagementResourceId" }
if ($FabricWorkspaceResourceId) { Write-Host "Fabric: $FabricWorkspaceResourceId" }
Write-Host ""

# Verify Azure CLI is logged in
$azAccount = az account show -o json 2>$null | ConvertFrom-Json
if (-not $azAccount) {
    Write-Host "[FAIL] Not logged in to Azure CLI. Run: az login" -ForegroundColor Red
    exit 1
}

# Verify subscription access without switching the active context
$subCheck = az account show --subscription $SubscriptionId --query "id" -o tsv 2>$null
if (-not $subCheck) {
    Write-Host "[FAIL] Cannot access subscription $SubscriptionId. Verify the ID and your access." -ForegroundColor Red
    exit 1
}
# Ensure the CLI is already pointed at the right subscription
$activeSubId = ($azAccount.id).Trim()
if ($activeSubId -ne $SubscriptionId) {
    Write-Host "[FAIL] Active subscription ($activeSubId) does not match requested ($SubscriptionId)." -ForegroundColor Red
    Write-Host "       Run: az account set --subscription $SubscriptionId" -ForegroundColor Red
    exit 1
}

# Validate Location is a real Azure region
$validLocations = az account list-locations --query "[].name" -o tsv 2>$null
if ($validLocations) {
    $locationList = $validLocations -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    if ($locationList -contains $Location) {
        Pass "Location '$Location' is a valid Azure region"
    } else {
        Fail "Location '$Location' is not a valid Azure region. Run: az account list-locations --query ""[].name"" -o tsv"
        exit 1
    }
} else {
    Warn "Could not query Azure locations. Skipping location validation."
}

# =============================================================================
# 1. Resource Provider Registration
# =============================================================================
Write-Host "--- Resource Provider Registration ---"
$requiredProviders = @(
    'Microsoft.CognitiveServices',
    'Microsoft.Storage',
    'Microsoft.Search',
    'Microsoft.DocumentDB',
    'Microsoft.Network',
    'Microsoft.App',
    'Microsoft.KeyVault',
    'Microsoft.MachineLearningServices',
    'Microsoft.ContainerService'
)

# Optional providers — warn if not registered instead of failing
$optionalProviders = @{
    'Microsoft.Bing'              = 'Required only if using Grounding with Bing Search tool'
    'Microsoft.ApiManagement'     = 'Required only for APIM setups'
    'Microsoft.Web'               = 'Required only for agent tools setup (Azure Functions)'
    'Microsoft.ManagedIdentity'   = 'Required only for user-assigned identity setups'
    'Microsoft.ContainerRegistry' = 'Required when enableContainerRegistry is true (most templates)'
}

foreach ($rp in $requiredProviders) {
    $state = az provider show --namespace $rp --query "registrationState" -o tsv 2>$null
    if ($state -eq 'Registered') {
        Pass "$rp is registered"
    } else {
        Fail "$rp is NOT registered (state: $state). Run: az provider register --namespace '$rp'"
    }
}

foreach ($rp in $optionalProviders.Keys) {
    $state = az provider show --namespace $rp --query "registrationState" -o tsv 2>$null
    if ($state -eq 'Registered') {
        Pass "$rp is registered"
    } else {
        Warn "$rp is NOT registered. $($optionalProviders[$rp]). Run: az provider register --namespace '$rp'"
    }
}

# =============================================================================
# 2. Resource Group
# =============================================================================
Write-Host ""
Write-Host "--- Resource Group ---"
$rgExists = az group exists --name $ResourceGroup 2>$null

if ($rgExists -eq 'true') {
    $rgLocation = az group show --name $ResourceGroup --query "location" -o tsv 2>$null
    Pass "Resource group '$ResourceGroup' exists in '$rgLocation'"

    if ($rgLocation -ne $Location) {
        Warn "RG location ($rgLocation) differs from deployment location ($Location). Private endpoints use resourceGroup().location — this may cause cross-region failures."
    }

    # Check for existing AI accounts (orphan risk with timestamp-based naming)
    $existingAI = az cognitiveservices account list --resource-group $ResourceGroup --query "[].name" -o tsv 2>$null
    if ($existingAI) {
        Warn "Existing AI account(s) found: $existingAI. Re-deploying creates NEW resources with a different suffix. Pass existing resource IDs to reuse them."
    }

    # Soft-deleted accounts
    $deleted = az cognitiveservices account list-deleted `
        --query "[?contains(id, '/resourceGroups/$ResourceGroup/')].name" -o tsv 2>$null
    if ($deleted) {
        Warn "Soft-deleted AI account(s) found in this RG: $deleted. If the new deployment uses the same name, it will fail. Purge if needed: az cognitiveservices account purge --name <name> --resource-group $ResourceGroup --location $Location"
    }
} else {
    Pass "Resource group '$ResourceGroup' will be created"
}

# =============================================================================
# 3. BYO Resource Validation
# =============================================================================

# Helper: extract subscription and RG from a BYO resource ID and
# flag cross-subscription or cross-resource-group differences.
function Test-ByoResourceContext {
    param([string]$Id, [string]$Label)
    $segments = $Id -split '/'
    $byoSub = $segments[2]
    $byoRg  = $segments[4]

    # Cross-subscription
    if ($byoSub -ne $SubscriptionId) {
        Info "$Label is in a different subscription ($byoSub). Templates support cross-subscription BYO — ensure the deploying principal has read access."
    }
    # Cross-resource-group
    if ($byoRg -ne $ResourceGroup) {
        Info "$Label is in resource group '$byoRg' (deploying to '$ResourceGroup'). Private endpoints will be created in the deployment RG."
    }
}

if ($AiSearchResourceId -or $StorageAccountResourceId -or $CosmosDBResourceId) {
    Write-Host ""
    Write-Host "--- BYO Resource Validation ---"
}

# --- AI Search ---
if ($AiSearchResourceId) {
    if (Test-ArmResourceId -Id $AiSearchResourceId -Label "AI Search") {
        $searchInfo = az resource show --ids $AiSearchResourceId --query "{sku:sku.name, location:location}" -o json 2>$null | ConvertFrom-Json
        if ($searchInfo) {
            Pass "AI Search resource exists (SKU: $($searchInfo.sku), Location: $($searchInfo.location))"
            Test-ByoResourceContext -Id $AiSearchResourceId -Label "AI Search"
            if ($searchInfo.sku -eq 'free') {
                Fail "AI Search SKU is 'free'. Free tier does not support private endpoints. Use a dedicated tier (basic or higher)."
            }
            # Check AAD auth is enabled (replicates validate-search-aad-auth.bicep logic)
            $searchAuth = az search service show --ids $AiSearchResourceId --query "{disableLocalAuth:disableLocalAuth, authOptions:authOptions}" -o json 2>$null | ConvertFrom-Json
            if ($searchAuth) {
                if ($searchAuth.disableLocalAuth -eq $true) {
                    Pass "AI Search AAD auth: local auth disabled (AAD-only)"
                } elseif ($searchAuth.authOptions -and $searchAuth.authOptions.aadOrApiKey) {
                    Warn "AI Search has local auth enabled. Consider disabling it for private network deployments."
                    Write-Host "       Fix: az search service update --ids $AiSearchResourceId --disable-local-auth true" -ForegroundColor Yellow
                } else {
                    Fail "AI Search does not have AAD authentication enabled. The Bicep deployment will fail."
                    Write-Host "       Fix: az search service update --ids $AiSearchResourceId --auth-options aadOrApiKey --aad-auth-failure-mode http401WithBearerChallenge" -ForegroundColor Yellow
                }
            } else {
                Warn "Could not query AI Search auth settings. Skipping AAD auth check."
            }
        } else {
            Fail "AI Search resource not found or no access: $AiSearchResourceId"
        }
    }
}

# --- Storage Account ---
if ($StorageAccountResourceId) {
    if (Test-ArmResourceId -Id $StorageAccountResourceId -Label "Storage Account") {
        $storageInfo = az resource show --ids $StorageAccountResourceId --query "{kind:kind, location:location}" -o json 2>$null | ConvertFrom-Json
        if ($storageInfo) {
            Pass "Storage Account exists"
            Test-ByoResourceContext -Id $StorageAccountResourceId -Label "Storage Account"
            if ($storageInfo.kind -eq "StorageV2") {
                Pass "Storage Account kind is StorageV2"
            } else {
                Warn "Storage Account kind is '$($storageInfo.kind)'. Templates create StorageV2. Some features (e.g., file shares) may not work with other kinds."
            }
        } else {
            Fail "Storage Account not found or no access: $StorageAccountResourceId"
        }
    }
}

# --- Cosmos DB ---
if ($CosmosDBResourceId) {
    if (Test-ArmResourceId -Id $CosmosDBResourceId -Label "Cosmos DB") {
        $cosmosInfo = az cosmosdb show --ids $CosmosDBResourceId --query "{disableLocalAuth:disableLocalAuth, location:location}" -o json 2>$null | ConvertFrom-Json
        if ($cosmosInfo) {
            Pass "Cosmos DB exists (Location: $($cosmosInfo.location))"
            Test-ByoResourceContext -Id $CosmosDBResourceId -Label "Cosmos DB"
            if ($cosmosInfo.disableLocalAuth -ne $true) {
                Fail "Cosmos DB disableLocalAuth is not true. Foundry requires AAD-only auth. Fix: az resource update --ids $CosmosDBResourceId --set properties.disableLocalAuth=true"
            } else {
                Pass "Cosmos DB disableLocalAuth is enabled"
            }
        } else {
            Fail "Cosmos DB not found or no access: $CosmosDBResourceId"
        }
    }
}

# --- API Management (APIM private network setup - optional) ---
if ($ApiManagementResourceId) {
    if (Test-ArmResourceId -Id $ApiManagementResourceId -Label "API Management") {
        $apimInfo = az resource show --ids $ApiManagementResourceId --query "{id:id, location:location}" -o json 2>$null | ConvertFrom-Json
        if ($apimInfo) {
            Pass "API Management instance exists"
            Test-ByoResourceContext -Id $ApiManagementResourceId -Label "API Management"
        } else {
            Fail "API Management not found or no access: $ApiManagementResourceId"
        }
    }
}

# --- Fabric Workspace (agent tools / MCP setup - optional) ---
if ($FabricWorkspaceResourceId) {
    if (Test-ArmResourceId -Id $FabricWorkspaceResourceId -Label "Fabric Workspace") {
        $fabricInfo = az resource show --ids $FabricWorkspaceResourceId --query "{id:id, location:location}" -o json 2>$null | ConvertFrom-Json
        if ($fabricInfo) {
            Pass "Fabric Workspace exists"
            Test-ByoResourceContext -Id $FabricWorkspaceResourceId -Label "Fabric Workspace"
        } else {
            Fail "Fabric Workspace not found or no access: $FabricWorkspaceResourceId"
        }
    }
}

# =============================================================================
# 4. Existing VNet Validation
# =============================================================================
if ($ExistingVnetId) {
    Write-Host ""
    Write-Host "--- Existing VNet Validation ---"

    if ($ExistingVnetId -match 'resourceGroups/([^/]+).*virtualNetworks/([^/]+)') {
        $vnetRg = $Matches[1]
        $vnetName = $Matches[2]
    } else {
        Fail "Cannot parse VNet resource ID: $ExistingVnetId"
        $vnetRg = $null
    }

    if ($vnetRg) {
        $vnetLocation = az network vnet show --resource-group $vnetRg --name $vnetName --query "location" -o tsv 2>$null
        if ($vnetLocation) {
            Pass "VNet '$vnetName' exists in '$vnetLocation'"
            if ($vnetLocation -ne $Location) {
                Fail "VNet location ($vnetLocation) differs from deployment location ($Location). PEs must be in the same region as the VNet."
            }
        } else {
            Fail "VNet '$vnetName' not found in RG '$vnetRg'"
        }

        # Check subnets — which subnets matter depends on the template:
        #   pe-subnet:    all private-network templates
        #   agent-subnet: VNet-injection — NOT managed network
        #   mcp-subnet:   only agent tools
        # The script doesn't know the target template, so it checks all three
        # and gives per-subnet guidance when one is missing.
        $subnetNotes = @{
            'pe-subnet'    = 'Required by all private-network templates. Most templates create it automatically if deploying a new VNet.'
            'agent-subnet' = 'Required by VNet-injection. Not used by managed network. Created automatically when deploying a new VNet.'
            'mcp-subnet'   = 'Only required by agent tools with MCP.'
        }
        foreach ($subnetName in @('agent-subnet', 'pe-subnet', 'mcp-subnet')) {
            $subnetJson = az network vnet subnet show --resource-group $vnetRg --vnet-name $vnetName --name $subnetName -o json 2>$null
            if ($subnetJson) {
                $subnet = $subnetJson | ConvertFrom-Json
                Pass "Subnet '$subnetName' exists ($($subnet.addressPrefix))"

                # SAL check
                $salCount = ($subnet.serviceAssociationLinks | Measure-Object).Count
                if ($salCount -gt 0) {
                    $salType = $subnet.serviceAssociationLinks[0].linkedResourceType
                    Fail "Subnet '$subnetName' has a serviceAssociationLink held by '$salType'. Deploying to this subnet will fail — the platform cannot inject into a subnet already owned by another resource."
                }

                # Delegation check for agent/mcp subnets
                if ($subnetName -eq 'agent-subnet' -or $subnetName -eq 'mcp-subnet') {
                    $delegation = if ($subnet.delegations) { $subnet.delegations[0].serviceName } else { $null }
                    if ($delegation -eq 'Microsoft.App/environments') {
                        Pass "Subnet '$subnetName' delegation: $delegation"
                    } else {
                        Fail "Subnet '$subnetName' must be delegated to Microsoft.App/environments (current: $(if ($delegation) { $delegation } else { 'none' }))"
                    }
                }
            } else {
                Warn "Subnet '$subnetName' does not exist. $($subnetNotes[$subnetName])"
            }
        }

        # PE subnet capacity check
        $peSubnet = az network vnet subnet show --resource-group $vnetRg --vnet-name $vnetName --name 'pe-subnet' -o json 2>$null | ConvertFrom-Json
        if ($peSubnet) {
            $prefix = $peSubnet.addressPrefix
            if ($prefix -match '/(\d+)$') {
                $cidrBits = [int]$Matches[1]
                $totalIPs = [math]::Pow(2, 32 - $cidrBits)
                $usableIPs = $totalIPs - 5  # Azure reserves 5 IPs per subnet
                # Base templates create 4 PEs (AI Services, Search, Storage, CosmosDB).
                # APIM and agent-tools setups add additional PEs.
                $requiredPEs = 4
                if ($ApiManagementResourceId) { $requiredPEs++ }
                if ($FabricWorkspaceResourceId) { $requiredPEs++ }
                if ($usableIPs -lt $requiredPEs) {
                    Fail "PE subnet /$cidrBits has ~$usableIPs usable IPs but template needs at least $requiredPEs. Use /28 minimum (/24 recommended)."
                } else {
                    Pass "PE subnet /$cidrBits has ~$usableIPs usable IPs (need $requiredPEs)"
                }
            }
        }
    }
}

# =============================================================================
# 5. DNS Zone Conflict Check
# =============================================================================
Write-Host ""
Write-Host "--- DNS Zone Conflicts ---"
$dnsZones = @{
    'privatelink.services.ai.azure.com'     = 'Used by all private-network setups for AI Services PE.'
    'privatelink.openai.azure.com'          = 'Used by all private-network setups for OpenAI PE.'
    'privatelink.cognitiveservices.azure.com'= 'Used by all private-network setups for Cognitive Services PE.'
    'privatelink.search.windows.net'        = 'Used by all private-network setups for AI Search PE.'
    'privatelink.blob.core.windows.net'     = 'Used by all private-network setups for Storage PE.'
    'privatelink.documents.azure.com'       = 'Used by all private-network setups for Cosmos DB PE.'
    'privatelink.azurecr.io'                = 'Used when enableContainerRegistry is true (most templates).'
    'privatelink.azure-api.net'             = 'Only needed for APIM setups.'
    'privatelink.fabric.microsoft.com'      = 'Only needed for agent tools with Fabric connection.'
}

if ($rgExists -eq 'true') {
    $existingZones = az network private-dns zone list --resource-group $ResourceGroup --query "[].name" -o tsv 2>$null
    $dnsConflicts = 0
    foreach ($zone in $dnsZones.Keys) {
        if ($existingZones -and ($existingZones -split "`n" | Where-Object { $_.Trim() -eq $zone })) {
            Warn "DNS zone '$zone' already exists in $ResourceGroup. $($dnsZones[$zone]) Template may fail on VNet link creation if zone is already linked."
            $dnsConflicts++
        }
    }
    if ($dnsConflicts -eq 0) {
        Pass "No DNS zone conflicts found"
    }
} else {
    Pass "New resource group — no DNS zone conflicts possible"
}

# =============================================================================
# 6. Quota Checks
# =============================================================================
if ($ModelName) {
    Write-Host ""
    Write-Host "--- Quota Checks ---"

    # 6a. Model availability
    $modelAvailable = az cognitiveservices model list --location $Location `
        --query "[?model.name=='$ModelName' && model.format=='$ModelFormat'].model.name" -o tsv 2>$null | Select-Object -First 1

    if ($modelAvailable) {
        Pass "Model '$ModelName' ($ModelFormat) is available in $Location"
    } else {
        Fail "Model '$ModelName' ($ModelFormat) is NOT available in $Location. Check region support or choose a different model."
    }

    # 6b. Model quota (TPM capacity)
    if ($modelAvailable -and $ModelSkuName -and $ModelCapacity -gt 0) {
        # Quota name pattern: {QuotaPrefix}.{Sku}.{Model} e.g. OpenAI.Standard.gpt-4o, AIServices.GlobalStandard.Mistral-Large-3
        # Note: Azure sometimes strips hyphens from model names (e.g. gpt-4.1 -> gpt4.1) so we try both.
        $quotaPrefix = if ($ModelFormat -eq 'OpenAI') { 'OpenAI' } else { 'AIServices' }
        $quotaName = "$quotaPrefix.$ModelSkuName.$ModelName"
        $quotaInfo = az cognitiveservices usage list --location $Location `
            --query "[?name.value=='$quotaName']" -o json 2>$null | ConvertFrom-Json
        if (-not $quotaInfo -or $quotaInfo.Count -eq 0) {
            $altName = "$quotaPrefix.$ModelSkuName.$($ModelName -replace '-','')" 
            if ($altName -ne $quotaName) {
                $quotaInfo = az cognitiveservices usage list --location $Location `
                    --query "[?name.value=='$altName']" -o json 2>$null | ConvertFrom-Json
                if ($quotaInfo -and $quotaInfo.Count -gt 0) { $quotaName = $altName }
            }
        }
        if ($quotaInfo -and $quotaInfo.Count -gt 0) {
            $currentUsage = $quotaInfo[0].currentValue
            $limit = $quotaInfo[0].limit
            $remaining = $limit - $currentUsage
            if ($remaining -ge $ModelCapacity) {
                Pass "Model quota: $remaining TPM available out of $limit ($ModelSkuName) — requesting $ModelCapacity"
            } else {
                Fail "Model quota insufficient: $remaining TPM available out of $limit ($ModelSkuName) — need $ModelCapacity. Request increase at https://aka.ms/oai/quotaincrease"
            }
        } else {
            Warn "Could not query quota for $ModelName/$ModelSkuName in $Location. Verify manually."
        }
    }
} # end if ($ModelName)

# 6c. Cosmos DB throughput check (BYO)
if ($CosmosDBResourceId) {
    $cosmosRg = ($CosmosDBResourceId -split '/')[4]
    $cosmosName = ($CosmosDBResourceId -split '/')[-1]
    $offerInfo = az cosmosdb show --name $cosmosName --resource-group $cosmosRg `
        --query "{enableAutomaticFailover:enableAutomaticFailover, totalThroughputLimit:totalThroughputLimit}" -o json 2>$null | ConvertFrom-Json
    if ($offerInfo) {
        # Each project needs 3 containers × 1000 RU/s = 3000 RU/s minimum
        $minRUs = 3000
        if ($offerInfo.totalThroughputLimit -and $offerInfo.totalThroughputLimit -gt 0 -and $offerInfo.totalThroughputLimit -lt $minRUs) {
            Fail "Cosmos DB total throughput limit is $($offerInfo.totalThroughputLimit) RU/s. Agent service requires at least $minRUs RU/s per project (3 containers × 1000 RU/s)."
        } else {
            Pass "Cosmos DB throughput limit: $(if ($offerInfo.totalThroughputLimit -and $offerInfo.totalThroughputLimit -gt 0) { "$($offerInfo.totalThroughputLimit) RU/s" } else { 'unlimited' }) (need $minRUs per project)"
        }
    }
}

# =============================================================================
# 7. Resource Quota Checks (per region)
# =============================================================================
Write-Host ""
Write-Host "--- Resource Quotas ($Location) ---"

# 7a. AI Search service quota (only when template will create a new one)
if ($AiSearchResourceId) {
    Pass "AI Search quota check skipped — using BYO resource"
} else {
    # Templates default to 'standard' SKU when creating AI Search
    $searchSkuToCheck = 'standard'
    $searchQuota = az rest --method GET `
        --url "https://management.azure.com/subscriptions/$SubscriptionId/providers/Microsoft.Search/locations/$Location/usages?api-version=2024-06-01-preview" `
        --query "value[?name.value=='$searchSkuToCheck']" -o json 2>$null | ConvertFrom-Json
    if ($searchQuota -and $searchQuota.Count -gt 0) {
        $searchCurrent = [int]$searchQuota[0].currentValue
        $searchLimit = [int]$searchQuota[0].limit
        if ($searchLimit -eq 0) {
            Warn "AI Search $searchSkuToCheck tier has 0 quota in $Location. Search service creation will fail."
        } elseif ($searchCurrent -ge $searchLimit) {
            Fail "AI Search $searchSkuToCheck tier quota exhausted in ${Location}: $searchCurrent/$searchLimit. Request increase or use a different region."
        } else {
            Pass "AI Search $searchSkuToCheck quota in ${Location}: $searchCurrent/$searchLimit"
        }
    } else {
        Warn "Could not query AI Search quota for $Location."
    }

    # 7a-note. AI Search capacity limitation
    # Azure does not expose a public API for physical capacity checks on Search.
    # Quota (above) checks service-count limits, but a region can have quota available
    # and still fail with 'InsufficientResourcesAvailable' when physical capacity is exhausted.
    # ARM deployment validate does NOT catch this either — it only validates template schema.
    # If Search creation fails mid-deploy, try a different region.
    Info "No API exists to pre-check AI Search physical capacity. If deployment fails with 'InsufficientResourcesAvailable', try a different region."
}

# 7b. Storage account quota (only when template will create a new one)
if ($StorageAccountResourceId) {
    Pass "Storage account quota check skipped — using BYO resource"
} else {
    $storageQuotaRaw = az storage account list --query "length([?location=='$Location'])" -o tsv 2>$null
    $storageCount = 0
    if ($storageQuotaRaw -match '^\d+$') {
        $storageCount = [int]$storageQuotaRaw
        # Default limit is 250 per region per subscription
        if ($storageCount -ge 250) {
            Fail "Storage account limit reached in ${Location}: $storageCount/250."
        } elseif ($storageCount -ge 200) {
            Warn "Storage accounts in ${Location}: $storageCount/250 — approaching limit."
        } else {
            Pass "Storage accounts in ${Location}: $storageCount/250"
        }
    }
}

# 7c. Network quotas (VNets, PEs)
# Skip VNet quota if BYO VNet provided; always check PE quota (templates create PEs regardless)
if ($ExistingVnetId) {
    Pass "VNet quota check skipped — using BYO VNet"
    $netQuotaFilter = "[?name.value=='PrivateEndpoints'].{name:name.localizedValue, current:currentValue, limit:limit}"
} else {
    $netQuotaFilter = "[?name.value=='VirtualNetworks' || name.value=='PrivateEndpoints'].{name:name.localizedValue, current:currentValue, limit:limit}"
}
$netQuotas = az network list-usages --location $Location `
    --query $netQuotaFilter `
    -o json 2>$null | ConvertFrom-Json
if ($netQuotas) {
    foreach ($q in $netQuotas) {
        $currentVal = [int]$q.current
        $limitVal = [int]$q.limit
        $pct = if ($limitVal -gt 0) { [math]::Round(($currentVal / $limitVal) * 100) } else { 0 }
        if ($currentVal -ge $limitVal) {
            Fail "$($q.name) quota exhausted in ${Location}: $currentVal/$limitVal"
        } elseif ($pct -ge 80) {
            Warn "$($q.name) in ${Location}: $currentVal/$limitVal ($pct% used)"
        } else {
            Pass "$($q.name) in ${Location}: $currentVal/$limitVal"
        }
    }
}

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
Write-Host "========================================"
Write-Host "Results: " -NoNewline
Write-Host "$($script:PassCount) passed" -ForegroundColor Green -NoNewline
Write-Host ", " -NoNewline
Write-Host "$($script:FailCount) failed" -ForegroundColor Red -NoNewline
Write-Host ", " -NoNewline
Write-Host "$($script:WarnCount) warnings" -ForegroundColor Yellow
Write-Host "========================================"

Write-Host ""
Write-Host "NOTE: Region support and IP range restrictions vary by scenario." -ForegroundColor Cyan
Write-Host "Before deploying, verify your region and subnet ranges against:" -ForegroundColor Cyan
Write-Host "  - Managed VNet regions:   https://learn.microsoft.com/azure/foundry/how-to/managed-virtual-network#limitations" -ForegroundColor Cyan
Write-Host "  - Private networking:     https://learn.microsoft.com/azure/foundry/agents/how-to/virtual-networks#limitations" -ForegroundColor Cyan

if ($script:FailCount -gt 0) {
    Write-Host "Fix the failures above before deploying." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Pre-checks passed. Safe to deploy." -ForegroundColor Green
    exit 0
}
