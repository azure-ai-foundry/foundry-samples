<#
.SYNOPSIS
    Post-deployment diagnostic for Foundry private network setups.

.DESCRIPTION
    Runs after 'az deployment group create' or 'terraform apply' to validate
    that all resources, networking, RBAC, and capability hosts are healthy.

    Checks are ordered outside-in, following the network path an agent
    request takes from the Data Proxy through the VNet to backend resources:

    1.  Discovery — find AI Services accounts
    2.  Network Injection (Data Proxy) — is the platform infra alive?
    3.  VNet, Subnets, Delegations, SAL — network plumbing
    4.  NSG Rules — can traffic flow between subnets?
    5.  DNS Zones — zone existence + VNet links
    6.  Custom DNS Detection — forwarder requirements
    7.  Private Endpoints + Network Rules — PE connectivity
    8.  Projects & Managed Identity — project exists with MI
    9.  Capability Host — caphost healthy + connections wired
    10. Project Connections — Cosmos/Storage/Search connections exist
    11. RBAC Role Assignments — MI has required roles
    12. Resource Provisioning State — all resources healthy
    13. Public Network Access + AI Services ACLs — lockdown audit
    14. Model Deployment — models ready
    15. Azure Policy — nothing blocking

    Use this script to quickly pinpoint why agents fail after a seemingly
    successful deployment.

.PARAMETER ConfigFile
    Path to a config file with key=value pairs. See diagnostic.config.sample.

.PARAMETER SubscriptionId
    Azure subscription ID

.PARAMETER ResourceGroup
    Resource group containing the deployed resources

.PARAMETER AccountName
    Optional. AI Services account name. Auto-discovered if omitted.

.EXAMPLE
    .\diagnostic-check.ps1 -ConfigFile .\diagnostic.config

.EXAMPLE
    .\diagnostic-check.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg"

.EXAMPLE
    .\diagnostic-check.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg" -AccountName "aifoundryabcd"
#>

#Requires -Version 7.0

param(
    [string]$ConfigFile = '',
    [string]$SubscriptionId = '',
    [string]$ResourceGroup = '',
    [string]$AccountName = ''
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
    if (-not $SubscriptionId -and $config['SubscriptionId']) { $SubscriptionId = $config['SubscriptionId'] }
    if (-not $ResourceGroup -and $config['ResourceGroup']) { $ResourceGroup = $config['ResourceGroup'] }
    if (-not $AccountName -and $config['AccountName']) { $AccountName = $config['AccountName'] }
}

# --- Validate required params ---
if (-not $SubscriptionId -or -not $ResourceGroup) {
    Write-Host "ERROR: SubscriptionId and ResourceGroup are required." -ForegroundColor Red
    Write-Host "Usage:"
    Write-Host "  .\diagnostic-check.ps1 -ConfigFile .\diagnostic.config"
    Write-Host "  .\diagnostic-check.ps1 -SubscriptionId 'xxx' -ResourceGroup 'my-rg'"
    exit 1
}

$ScriptVersion = "1.0.0"
$ErrorActionPreference = "Continue"
$script:PassCount = 0
$script:FailCount = 0
$script:WarnCount = 0

function Pass  { param([string]$Msg) Write-Host "[PASS] $Msg" -ForegroundColor Green; $script:PassCount++ }
function Fail  { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:FailCount++ }
function Warn  { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow; $script:WarnCount++ }
function Info  { param([string]$Msg) Write-Host "[INFO] $Msg" -ForegroundColor Cyan }
function Detail { param([string]$Msg) Write-Host "       $Msg" -ForegroundColor Gray }

Write-Host ""
Write-Host "========================================"
Write-Host "Post-Deployment Diagnostic (outside-in)"
Write-Host "========================================"
Write-Host "Version:        $ScriptVersion"
Write-Host "Subscription:   $SubscriptionId"
Write-Host "Resource Group: $ResourceGroup"
if ($AccountName) { Write-Host "Account:        $AccountName" }
Write-Host "Timestamp:      $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC' -AsUTC)"
Write-Host "========================================"
Write-Host ""

# Verify Azure CLI login
$azAccount = az account show -o json 2>$null | ConvertFrom-Json
if (-not $azAccount) {
    Write-Host "[FAIL] Not logged in to Azure CLI. Run: az login" -ForegroundColor Red
    exit 1
}

$activeSubId = ($azAccount.id).Trim()
if ($activeSubId -ne $SubscriptionId) {
    Write-Host "[FAIL] Active subscription ($activeSubId) does not match requested ($SubscriptionId)." -ForegroundColor Red
    Write-Host "       Run: az account set --subscription $SubscriptionId" -ForegroundColor Red
    exit 1
}

# ARM API version
$CogApiVersion = "2025-04-01-preview"

function Get-AzToken {
    az account get-access-token --query accessToken -o tsv 2>$null
}

function Invoke-ArmGet {
    param([string]$Url)
    $token = Get-AzToken
    $headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
    try {
        $resp = Invoke-RestMethod -Uri $Url -Headers $headers -Method Get -ErrorAction Stop
        return $resp
    } catch {
        return $null
    }
}

# --- Helper: check PE connections on a resource ---
function Test-PEConnections {
    param([string]$ResourceId, [string]$Label)
    $peList = az network private-endpoint-connection list --id $ResourceId -o json 2>$null | ConvertFrom-Json
    $approvedCount = 0
    if ($peList -and $peList.Count -gt 0) {
        foreach ($pec in $peList) {
            $pecName = $pec.name
            if (-not $pecName -and $pec.id) { $pecName = ($pec.id -split '/')[-1] }
            # Handle both nested (ARM REST) and flat (az CLI) property structures
            $pecStatus = $pec.properties.privateLinkServiceConnectionState.status
            if (-not $pecStatus) { $pecStatus = $pec.privateLinkServiceConnectionState.status }
            if ($pecStatus -eq 'Approved') {
                Pass "$Label PE '$pecName': $pecStatus"
                $approvedCount++
            } elseif ($pecStatus -eq 'Pending') {
                Fail "$Label PE '$pecName': $pecStatus — needs manual approval"
            } else {
                Fail "$Label PE '$pecName': $pecStatus"
            }
        }
    }
    return $approvedCount
}

# =============================================================================
# 1. DISCOVER AI SERVICES ACCOUNTS
# =============================================================================
Write-Host "--- 1. Discover AI Services Accounts ---"

if ($AccountName) {
    $accounts = @(@{ name = $AccountName })
    Info "Using provided account: $AccountName"
} else {
    $accountsJson = az cognitiveservices account list --resource-group $ResourceGroup --query "[?kind=='AIServices']" -o json 2>$null | ConvertFrom-Json
    if (-not $accountsJson -or $accountsJson.Count -eq 0) {
        Fail "No AIServices accounts found in resource group '$ResourceGroup'"
        Write-Host ""
        Write-Host "========================================"
        Write-Host "Results: $($script:PassCount) passed, $($script:FailCount) failed, $($script:WarnCount) warnings"
        Write-Host "========================================"
        exit 1
    }
    $accounts = $accountsJson
    Pass "Found $($accounts.Count) AIServices account(s): $($accounts.name -join ', ')"
}
Write-Host ""

foreach ($acct in $accounts) {
    $acctName = $acct.name
    Write-Host "========== Account: $acctName =========="
    Write-Host ""

    # Get full account details
    $acctDetail = az cognitiveservices account show --name $acctName --resource-group $ResourceGroup -o json 2>$null | ConvertFrom-Json
    if (-not $acctDetail) {
        Fail "Cannot retrieve account '$acctName'. It may have been deleted or you lack access."
        continue
    }

    $acctLocation = $acctDetail.location

    # Enumerate all resources in the primary RG
    $allResources = az resource list --resource-group $ResourceGroup -o json 2>$null | ConvertFrom-Json

    # Extract typed resources for use across sections
    $storageAccounts = @($allResources | Where-Object { $_.type -eq 'Microsoft.Storage/storageAccounts' })
    $cosmosAccounts = @($allResources | Where-Object { $_.type -eq 'Microsoft.DocumentDB/databaseAccounts' })
    $searchServices = @($allResources | Where-Object { $_.type -eq 'Microsoft.Search/searchServices' })
    $apimServices = @($allResources | Where-Object { $_.type -eq 'Microsoft.ApiManagement/service' })
    $containerRegistries = @($allResources | Where-Object { $_.type -eq 'Microsoft.ContainerRegistry/registries' })
    $vnets = @($allResources | Where-Object { $_.type -eq 'Microsoft.Network/virtualNetworks' })

    # Track which RGs we've already scanned to avoid duplicate lookups
    $scannedRGs = @($ResourceGroup)
    # Track BYO resource groups discovered
    $byoRGs = @()

    # --- BYO discovery 1: VNet RG from network injection subnetArmId ---
    $networkInjections = $acctDetail.properties.networkInjections
    if ($networkInjections) {
        foreach ($ni in $networkInjections) {
            $subnetId = $ni.subnetArmId
            if ($subnetId -and $subnetId -match '/subscriptions/[^/]+/resourceGroups/([^/]+)/') {
                $vnetRG = $Matches[1]
                if ($vnetRG -ne $ResourceGroup -and $scannedRGs -notcontains $vnetRG) {
                    Info "BYO VNet detected in RG '$vnetRG' (from network injection subnetArmId)"
                    $byoRGs += $vnetRG
                    $scannedRGs += $vnetRG
                    # Fetch VNet by parsing the full VNet resource ID from the subnet ID
                    $vnetId = ($subnetId -replace '/subnets/[^/]+$', '')
                    $byoVnet = az resource show --ids $vnetId -o json 2>$null | ConvertFrom-Json
                    if ($byoVnet) {
                        $vnets += $byoVnet
                    }
                }
            }
        }
    }

    # --- BYO discovery 2: Resources from project connection targets ---
    # Pre-scan all project connections to discover BYO storage/cosmos/search
    $projectsPreUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$acctName/projects?api-version=$CogApiVersion"
    $projectsPre = Invoke-ArmGet -Url $projectsPreUrl

    if ($projectsPre -and $projectsPre.value) {
        foreach ($proj in $projectsPre.value) {
            # ARM returns name as 'accountName/projectName' — extract just the project part
            $projNamePre = ($proj.name -split '/')[-1]
            $connPreUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$acctName/projects/$projNamePre/connections?api-version=$CogApiVersion"
            $connPre = Invoke-ArmGet -Url $connPreUrl
            if (-not $connPre -or -not $connPre.value) { continue }

            foreach ($conn in $connPre.value) {
                $connCat = $conn.properties.category
                $connTarget = $conn.properties.target
                if (-not $connTarget) { continue }

                # Parse resource name from connection target URL
                $resourceName = $null
                $resourceType = $null
                switch ($connCat) {
                    'AzureStorageAccount' {
                        # Target: https://<name>.blob.core.windows.net
                        if ($connTarget -match 'https://([^.]+)\.blob\.core\.windows\.net') {
                            $resourceName = $Matches[1]
                            $resourceType = 'Microsoft.Storage/storageAccounts'
                        }
                    }
                    { $_ -in 'AzureCosmosDBNoSQL', 'CosmosDb' } {
                        # Target: https://<name>.documents.azure.com:443/
                        if ($connTarget -match 'https://([^.]+)\.documents\.azure\.com') {
                            $resourceName = $Matches[1]
                            $resourceType = 'Microsoft.DocumentDB/databaseAccounts'
                        }
                    }
                    'CognitiveSearch' {
                        # Target: https://<name>.search.windows.net
                        if ($connTarget -match 'https://([^.]+)\.search\.windows\.net') {
                            $resourceName = $Matches[1]
                            $resourceType = 'Microsoft.Search/searchServices'
                        }
                    }
                }

                if (-not $resourceName -or -not $resourceType) { continue }

                # Check if already in our typed arrays
                $alreadyKnown = $false
                switch ($resourceType) {
                    'Microsoft.Storage/storageAccounts' { $alreadyKnown = ($storageAccounts | Where-Object { $_.name -eq $resourceName }).Count -gt 0 }
                    'Microsoft.DocumentDB/databaseAccounts' { $alreadyKnown = ($cosmosAccounts | Where-Object { $_.name -eq $resourceName }).Count -gt 0 }
                    'Microsoft.Search/searchServices' { $alreadyKnown = ($searchServices | Where-Object { $_.name -eq $resourceName }).Count -gt 0 }
                }
                if ($alreadyKnown) { continue }

                # Look up the resource subscription-wide by name and type
                $byoResource = az resource list --name $resourceName --resource-type $resourceType -o json 2>$null | ConvertFrom-Json
                if ($byoResource -and $byoResource.Count -gt 0) {
                    $byo = $byoResource[0]
                    $byoRGName = ($byo.id -split '/')[4]  # resourceGroups/<name>
                    Info "BYO $connCat '$resourceName' discovered in RG '$byoRGName' (from project connection)"
                    if ($byoRGs -notcontains $byoRGName) { $byoRGs += $byoRGName }
                    switch ($resourceType) {
                        'Microsoft.Storage/storageAccounts' { $storageAccounts += $byo }
                        'Microsoft.DocumentDB/databaseAccounts' { $cosmosAccounts += $byo }
                        'Microsoft.Search/searchServices' { $searchServices += $byo }
                    }
                } else {
                    Warn "Connection target '$resourceName' ($connCat) not found in subscription — may be in another subscription"
                }
            }
        }
    }

    if ($byoRGs.Count -gt 0) {
        Info "BYO resource groups discovered: $($byoRGs -join ', ')"
    }

    # =============================================================================
    # 2. NETWORK INJECTION (DATA PROXY) — Is the platform infra alive?
    # =============================================================================
    Write-Host "--- 2. Network Injection (Data Proxy) ---"

    $networkInjections = $acctDetail.properties.networkInjections
    # Collect injected subnet IDs for subnet role identification in sections 3 & 4
    $injectedSubnetIds = @()
    if ($networkInjections -and $networkInjections.Count -gt 0) {
        foreach ($ni in $networkInjections) {
            $scenario = $ni.scenario
            $subnetId = $ni.subnetArmId
            $useManagedNet = $ni.useMicrosoftManagedNetwork
            if ($subnetId) { $injectedSubnetIds += $subnetId.ToLower() }
            Pass "Network injection: scenario=$scenario, useManagedNetwork=$useManagedNet"
            if ($subnetId) {
                Info "  Subnet: $subnetId"
                # Verify the subnet exists
                $subnetCheck = az resource show --ids $subnetId -o json 2>$null | ConvertFrom-Json
                if ($subnetCheck) {
                    Pass "  Injected subnet exists"
                } else {
                    Fail "  Injected subnet NOT found — network injection will fail"
                }
            }
        }
    } else {
        Info "No network injections configured (Managed VNet or non-agent setup)"
    }
    Write-Host ""

    # =============================================================================
    # 3. VNET, SUBNETS, DELEGATIONS, AND SAL
    # =============================================================================
    Write-Host "--- 3. VNet, Subnets, Delegations, and SAL ---"

    foreach ($vnet in $vnets) {
        $vnetDetail = az network vnet show --ids $vnet.id -o json 2>$null | ConvertFrom-Json
        Pass "VNet '$($vnet.name)' found in $($vnetDetail.location)"

        foreach ($subnet in $vnetDetail.subnets) {
            $sName = $subnet.name
            $delegations = $subnet.delegations
            $sals = $subnet.serviceAssociationLinks
            $subnetFullId = $subnet.id.ToLower()

            # Identify subnet role by properties, not names
            $hasAppEnvDelegation = $false
            $hasDnsResolverDelegation = $false
            $hasWebDelegation = $false
            $otherDelegations = @()
            if ($delegations) {
                foreach ($d in $delegations) {
                    $dServiceName = $d.properties.serviceName ?? $d.serviceName
                    if ($dServiceName -eq 'Microsoft.App/environments') {
                        $hasAppEnvDelegation = $true
                    } elseif ($dServiceName -eq 'Microsoft.Network/dnsResolvers') {
                        $hasDnsResolverDelegation = $true
                    } elseif ($dServiceName -eq 'Microsoft.Web/serverFarms') {
                        $hasWebDelegation = $true
                    } elseif ($dServiceName) {
                        $otherDelegations += $dServiceName
                    }
                }
            }

            # Agent subnet = Microsoft.App/environments delegation AND referenced by network injection
            $isAgentSubnet = $hasAppEnvDelegation -and ($injectedSubnetIds -contains $subnetFullId)
            # MCP/other Container Apps subnet = same delegation but NOT the injected agent subnet
            $isContainerAppsSubnet = $hasAppEnvDelegation -and -not $isAgentSubnet
            # Web app subnet
            $isWebAppSubnet = $hasWebDelegation
            # PE subnet = no delegation at all
            $isPeSubnet = (-not $delegations -or $delegations.Count -eq 0)

            if ($isAgentSubnet) {
                Pass "Subnet '$sName': agent subnet (delegated to Microsoft.App/environments, referenced by network injection)"
            } elseif ($isContainerAppsSubnet) {
                Pass "Subnet '$sName': Container Apps subnet (delegated to Microsoft.App/environments, not the agent subnet)"
            } elseif ($isWebAppSubnet) {
                Pass "Subnet '$sName': web app subnet (delegated to Microsoft.Web/serverFarms — VNet-integrated app)"
            } elseif ($hasDnsResolverDelegation) {
                Pass "Subnet '$sName': delegated to Microsoft.Network/dnsResolvers"
            } elseif ($isPeSubnet) {
                Pass "Subnet '$sName': no delegation (PE or general-purpose subnet)"
            } else {
                foreach ($od in $otherDelegations) {
                    Info "Subnet '$sName': delegated to $od"
                }
            }

            # Check SAL (ServiceAssociationLink)
            if ($sals) {
                foreach ($sal in $sals) {
                    $salType = $sal.properties.linkedResourceType ?? $sal.linkedResourceType
                    $salAllowDelete = $sal.properties.allowDelete ?? $sal.allowDelete
                    if ($isAgentSubnet -and $salType -eq 'Microsoft.App/environments') {
                        Info "Subnet '$sName' has SAL: $salType (allowDelete=$salAllowDelete) — expected for active caphost"
                    } elseif ($hasDnsResolverDelegation -and $salType -eq 'Microsoft.Network/dnsResolvers') {
                        Info "Subnet '$sName' has SAL: $salType — expected for DNS Private Resolver"
                    } elseif ($isContainerAppsSubnet -and $salType -eq 'Microsoft.App/environments') {
                        Info "Subnet '$sName' has SAL: $salType — Container Apps environment bound"
                    } else {
                        Warn "Subnet '$sName' has unexpected SAL: $salType"
                    }
                }
            } elseif ($isAgentSubnet) {
                Warn "Agent subnet '$sName' has no SAL. Capability host may not have provisioned or was deleted."
            }
        }
    }
    if ($vnets.Count -eq 0) {
        Info "No VNets found in primary RG or via network injection (Managed VNet mode or BYO VNet in unlinked RG)"
    }
    Write-Host ""

    # =============================================================================
    # 4. NSG RULES — Can traffic flow between subnets?
    # =============================================================================
    Write-Host "--- 4. NSG Rules on Subnets ---"

    foreach ($vnet in $vnets) {
        $vnetDetail = az network vnet show --ids $vnet.id -o json 2>$null | ConvertFrom-Json
        foreach ($subnet in $vnetDetail.subnets) {
            $sName = $subnet.name
            $nsgRef = $subnet.networkSecurityGroup

            if (-not $nsgRef) {
                Info "Subnet '$sName': no NSG attached"
                continue
            }

            $nsgId = $nsgRef.id
            $nsgName = ($nsgId -split '/')[-1]
            Info "Subnet '$sName': NSG '$nsgName' attached"

            $nsgDetail = az network nsg show --ids $nsgId -o json 2>$null | ConvertFrom-Json
            if (-not $nsgDetail) {
                Fail "Cannot read NSG '$nsgName' (may be in another subscription or you lack access)"
                continue
            }

            # Combine default + custom rules
            # az network nsg show returns flat properties (e.g. $r.direction, $r.access)
            # ARM REST returns nested ($r.properties.direction) — handle both
            $allRules = @()
            if ($nsgDetail.securityRules) { $allRules += $nsgDetail.securityRules }
            if ($nsgDetail.defaultSecurityRules) { $allRules += $nsgDetail.defaultSecurityRules }

            # Helper to read rule property from flat or nested structure
            function Get-RuleProp($rule, $prop) {
                $val = $rule.properties.$prop
                if ($null -eq $val) { $val = $rule.$prop }
                return $val
            }

            # Identify subnet role by properties (same logic as section 3)
            $hasAppEnvDelegation = $false
            $hasWebDelegation = $false
            if ($subnet.delegations) {
                foreach ($d in $subnet.delegations) {
                    $dServiceName = $d.properties.serviceName ?? $d.serviceName
                    if ($dServiceName -eq 'Microsoft.App/environments') { $hasAppEnvDelegation = $true }
                    if ($dServiceName -eq 'Microsoft.Web/serverFarms') { $hasWebDelegation = $true }
                }
            }
            $subnetFullId = $subnet.id.ToLower()
            $isAgentSubnet = $hasAppEnvDelegation -and ($injectedSubnetIds -contains $subnetFullId)
            $isContainerAppsSubnet = $hasAppEnvDelegation -and -not $isAgentSubnet
            $isWebAppSubnet = $hasWebDelegation
            $isPeSubnet = (-not $subnet.delegations -or $subnet.delegations.Count -eq 0)

            # --- Check: Deny-all outbound blocks Azure services ---
            $denyAllOut = $allRules | Where-Object {
                (Get-RuleProp $_ 'direction') -eq 'Outbound' -and
                (Get-RuleProp $_ 'access') -eq 'Deny' -and
                (Get-RuleProp $_ 'destinationAddressPrefix') -eq '*' -and
                (Get-RuleProp $_ 'protocol') -eq '*'
            } | Sort-Object { [int](Get-RuleProp $_ 'priority') } | Select-Object -First 1

            if ($denyAllOut) {
                $denyPriority = [int](Get-RuleProp $denyAllOut 'priority')

                # Check if there's an Allow for HTTPS outbound to AzureCloud before the deny
                $allowAzureOut = $allRules | Where-Object {
                    (Get-RuleProp $_ 'direction') -eq 'Outbound' -and
                    (Get-RuleProp $_ 'access') -eq 'Allow' -and
                    [int](Get-RuleProp $_ 'priority') -lt $denyPriority -and
                    ((Get-RuleProp $_ 'destinationAddressPrefix') -match 'AzureCloud|VirtualNetwork|\*')
                }

                if ($allowAzureOut) {
                    Pass "NSG '$nsgName' on '$sName': has deny-all outbound but allows Azure traffic at higher priority"
                } else {
                    Fail "NSG '$nsgName' on '$sName': deny-all outbound (priority $denyPriority) with no Azure allow rule"
                    Detail "Agent/PE/MCP subnets need outbound HTTPS (443) to AzureCloud service tag"
                }
            }

            # --- Check: Required outbound ports ---
            if ($isAgentSubnet -or $isContainerAppsSubnet -or $isWebAppSubnet) {
                $subnetLabel = if ($isAgentSubnet) { 'agent' } elseif ($isContainerAppsSubnet) { 'Container Apps' } else { 'web app' }
                $requiredPorts = @('443')
                if ($isAgentSubnet -or $isContainerAppsSubnet) { $requiredPorts += '445' }
                foreach ($port in $requiredPorts) {
                    $blockRule = $allRules | Where-Object {
                        (Get-RuleProp $_ 'direction') -eq 'Outbound' -and
                        (Get-RuleProp $_ 'access') -eq 'Deny' -and
                        ((Get-RuleProp $_ 'destinationPortRange') -eq $port -or
                         ((Get-RuleProp $_ 'destinationPortRanges') -and (Get-RuleProp $_ 'destinationPortRanges') -contains $port))
                    }
                    if ($blockRule) {
                        Fail "NSG '$nsgName' on $subnetLabel subnet '$sName': explicitly blocks outbound port $port"
                        if ($port -eq '443') { Detail "Port 443 is required for Azure service communication (including AI Search, Storage, Cosmos)" }
                        if ($port -eq '445') { Detail "Port 445 is required for Azure Files (agent file share)" }
                    }
                }

                # Check outbound to VirtualNetwork (needed for PE connectivity from this subnet)
                $denyVnetOut = $allRules | Where-Object {
                    (Get-RuleProp $_ 'direction') -eq 'Outbound' -and
                    (Get-RuleProp $_ 'access') -eq 'Deny' -and
                    ((Get-RuleProp $_ 'destinationAddressPrefix') -eq 'VirtualNetwork') -and
                    [int](Get-RuleProp $_ 'priority') -lt 65000
                }
                if ($denyVnetOut) {
                    Fail "NSG '$nsgName' on $subnetLabel subnet '$sName': blocks outbound to VirtualNetwork — cannot reach private endpoints"
                    Detail "Resources like AI Search, Storage, and Cosmos are accessed via private endpoints within the VNet"
                }
            }

            # --- Check: PE subnet inbound from VNet ---
            if ($isPeSubnet) {
                $denyVnetIn = $allRules | Where-Object {
                    (Get-RuleProp $_ 'direction') -eq 'Inbound' -and
                    (Get-RuleProp $_ 'access') -eq 'Deny' -and
                    ((Get-RuleProp $_ 'sourceAddressPrefix') -eq 'VirtualNetwork' -or
                     (Get-RuleProp $_ 'sourceAddressPrefix') -eq '*') -and
                    ((Get-RuleProp $_ 'destinationPortRange') -eq '443' -or
                     (Get-RuleProp $_ 'destinationPortRange') -eq '*')
                } | Sort-Object { [int](Get-RuleProp $_ 'priority') } | Select-Object -First 1

                if ($denyVnetIn -and [int](Get-RuleProp $denyVnetIn 'priority') -lt 65000) {
                    $allowBefore = $allRules | Where-Object {
                        (Get-RuleProp $_ 'direction') -eq 'Inbound' -and
                        (Get-RuleProp $_ 'access') -eq 'Allow' -and
                        [int](Get-RuleProp $_ 'priority') -lt [int](Get-RuleProp $denyVnetIn 'priority') -and
                        ((Get-RuleProp $_ 'sourceAddressPrefix') -match 'VirtualNetwork|\*')
                    }
                    if (-not $allowBefore) {
                        Fail "NSG '$nsgName' on PE subnet '$sName': blocks inbound from VNet — PEs won't be reachable"
                    }
                }
            }

            # --- Check: Container Apps subnet inbound (MCP / other Container Apps) ---
            if ($isContainerAppsSubnet) {
                $denyMcpIn = $allRules | Where-Object {
                    (Get-RuleProp $_ 'direction') -eq 'Inbound' -and
                    (Get-RuleProp $_ 'access') -eq 'Deny' -and
                    (Get-RuleProp $_ 'sourceAddressPrefix') -eq '*' -and
                    (Get-RuleProp $_ 'destinationPortRange') -eq '*'
                } | Sort-Object { [int](Get-RuleProp $_ 'priority') } | Select-Object -First 1

                if ($denyMcpIn -and [int](Get-RuleProp $denyMcpIn 'priority') -lt 65000) {
                    $allowBefore = $allRules | Where-Object {
                        (Get-RuleProp $_ 'direction') -eq 'Inbound' -and
                        (Get-RuleProp $_ 'access') -eq 'Allow' -and
                        [int](Get-RuleProp $_ 'priority') -lt [int](Get-RuleProp $denyMcpIn 'priority') -and
                        ((Get-RuleProp $_ 'sourceAddressPrefix') -match 'VirtualNetwork')
                    }
                    if (-not $allowBefore) {
                        Warn "NSG '$nsgName' on Container Apps subnet '$sName': deny-all inbound with no VNet allow — tools may be unreachable from agents"
                    }
                }
            }

            # --- Summary of custom (non-default) rules ---
            $customRules = $nsgDetail.securityRules
            if ($customRules -and $customRules.Count -gt 0) {
                Info "  $($customRules.Count) custom rule(s) on '$nsgName':"
                foreach ($r in ($customRules | Sort-Object { $p = (Get-RuleProp $_ 'priority'); if ($p) { [int]$p } else { 0 } })) {
                    $rDir = Get-RuleProp $r 'direction'
                    $rAcc = Get-RuleProp $r 'access'
                    $rPri = Get-RuleProp $r 'priority'
                    $rDstPort = Get-RuleProp $r 'destinationPortRange'
                    $rDstPrefix = Get-RuleProp $r 'destinationAddressPrefix'
                    if (-not $rDir) { $rDir = '???' } else { $rDir = $rDir.Substring(0, [Math]::Min(3, $rDir.Length)) }
                    $acc = if ($rAcc -eq 'Allow') { 'Allow' } else { 'DENY' }
                    $dst = if ($rDstPort -eq '*') { 'all-ports' } else { "port:$rDstPort" }
                    Detail "  [$rPri] $rDir $acc $dst -> $rDstPrefix ($($r.name))"
                }
            } else {
                Pass "NSG '$nsgName': default rules only (no custom rules)"
            }
        }
    }
    Write-Host ""

    # =============================================================================
    # 5. PRIVATE DNS ZONES — Do names resolve to private IPs?
    # =============================================================================
    Write-Host "--- 5. Private DNS Zones ---"

    $expectedZones = @(
        'privatelink.services.ai.azure.com',
        'privatelink.openai.azure.com',
        'privatelink.cognitiveservices.azure.com',
        'privatelink.search.windows.net',
        'privatelink.blob.core.windows.net',
        'privatelink.documents.azure.com'
    )
    if ($apimServices.Count -gt 0) {
        $expectedZones += 'privatelink.azure-api.net'
    }
    if ($containerRegistries.Count -gt 0) {
        $expectedZones += 'privatelink.azurecr.io'
    }

    $dnsZones = az network private-dns zone list --resource-group $ResourceGroup -o json 2>$null | ConvertFrom-Json
    $foundZoneNames = @()
    $dnsZoneRGMap = @{}  # zone name -> RG where it was found
    if ($dnsZones) {
        foreach ($z in $dnsZones) {
            $foundZoneNames += $z.name
            $dnsZoneRGMap[$z.name] = $ResourceGroup
        }
    }

    # Also check BYO resource groups for DNS zones
    foreach ($byoRG in $byoRGs) {
        $byoDnsZones = az network private-dns zone list --resource-group $byoRG -o json 2>$null | ConvertFrom-Json
        if ($byoDnsZones) {
            foreach ($z in $byoDnsZones) {
                if ($foundZoneNames -notcontains $z.name) {
                    $foundZoneNames += $z.name
                    $dnsZoneRGMap[$z.name] = $byoRG
                    Info "DNS zone '$($z.name)' found in BYO RG '$byoRG'"
                }
            }
        }
    }

    foreach ($expected in $expectedZones) {
        if ($foundZoneNames -contains $expected) {
            $zoneRG = $dnsZoneRGMap[$expected]
            $rgLabel = if ($zoneRG -ne $ResourceGroup) { " (BYO RG: $zoneRG)" } else { "" }
            Pass "DNS zone '$expected': exists$rgLabel"

            # Check VNet links (in the RG where the zone was found)
            $links = az network private-dns link vnet list --zone-name $expected --resource-group $zoneRG -o json 2>$null | ConvertFrom-Json
            if ($links -and $links.Count -gt 0) {
                foreach ($link in $links) {
                    $linkState = $link.properties.provisioningState ?? $link.provisioningState
                    if ($linkState -eq 'Succeeded') {
                        Pass "  VNet link '$($link.name)': $linkState"
                    } elseif (-not $linkState) {
                        Info "  VNet link '$($link.name)': state unknown"
                    } else {
                        Fail "  VNet link '$($link.name)': $linkState"
                    }
                }
            } else {
                Fail "  DNS zone '$expected' has no VNet links — DNS resolution will fail"
            }
        } else {
            Warn "DNS zone '$expected': not found in RG or discovered BYO RGs (may be in a central DNS RG — verify it exists and is VNet-linked)"
        }
    }
    Write-Host ""

    # =============================================================================
    # 6. CUSTOM DNS SERVER DETECTION
    # =============================================================================
    Write-Host "--- 6. Custom DNS Server Detection ---"

    foreach ($vnet in $vnets) {
        $vnetDetail = az network vnet show --ids $vnet.id -o json 2>$null | ConvertFrom-Json
        $dnsServers = $vnetDetail.dhcpOptions.dnsServers

        if ($dnsServers -and $dnsServers.Count -gt 0) {
            Info "VNet '$($vnet.name)' uses custom DNS servers: $($dnsServers -join ', ') — ensure conditional forwarders for privatelink.* zones point to 168.63.129.16"
        } else {
            Pass "VNet '$($vnet.name)' uses Azure default DNS (168.63.129.16) — privatelink zones resolve automatically"
        }
    }
    Write-Host ""

    # =============================================================================
    # 7. PRIVATE CONNECTIVITY (PEs, Shared PEs, Network Rules)
    # =============================================================================
    Write-Host "--- 7. Private Endpoints and Network Rules ---"

    # AI Services PEs
    $aiPeCount = Test-PEConnections -ResourceId $acctDetail.id -Label "AI Services"
    if ($aiPeCount -eq 0) {
        Warn "No approved PE connections on AI Services account (expected for private setup)"
    }

    # Storage PEs + shared PE / resource access rules
    foreach ($sa in $storageAccounts) {
        $saDetail = az storage account show --ids $sa.id -o json 2>$null | ConvertFrom-Json
        $saPeCount = Test-PEConnections -ResourceId $sa.id -Label "Storage '$($sa.name)'"

        # Check resourceAccessRules (shared private endpoints from AI Services)
        $resourceAccessRules = $saDetail.networkRuleSet.resourceAccessRules
        $hasSharedPE = $false
        if ($resourceAccessRules -and $resourceAccessRules.Count -gt 0) {
            foreach ($rule in $resourceAccessRules) {
                $tenantId = $rule.tenantId
                $ruleResId = $rule.resourceId
                # Check if this grants access from the AI Services account
                if ($ruleResId -match 'Microsoft.CognitiveServices/accounts') {
                    Pass "Storage '$($sa.name)': shared PE / resource access rule allows AI Services ($ruleResId)"
                    $hasSharedPE = $true
                } else {
                    Info "Storage '$($sa.name)': resource access rule for $ruleResId"
                }
            }
        }

        # Network rules summary
        $saNetRules = $saDetail.networkRuleSet
        $saDefaultAction = $saNetRules.defaultAction
        $saBypass = $saNetRules.bypass

        if ($saDefaultAction -eq 'Deny') {
            Pass "Storage '$($sa.name)' network defaultAction: Deny"
        } else {
            Warn "Storage '$($sa.name)' network defaultAction: $saDefaultAction (expected Deny)"
        }

        if ($saBypass -match 'AzureServices') {
            Pass "Storage '$($sa.name)' bypass includes AzureServices (trusted services can access)"
        } else {
            Warn "Storage '$($sa.name)' bypass does NOT include AzureServices — AI Services trusted access blocked"
            if (-not $hasSharedPE -and $saPeCount -eq 0) {
                Fail "Storage '$($sa.name)' has no PE, no shared PE, and no AzureServices bypass — AI Services cannot reach it"
            }
        }

        # Shared key access
        $allowSharedKey = $saDetail.allowSharedKeyAccess
        if ($allowSharedKey -eq $false) {
            Pass "Storage '$($sa.name)' allowSharedKeyAccess: Disabled (AAD-only — correct)"
        } elseif ($allowSharedKey -eq $true) {
            Info "Storage '$($sa.name)' allowSharedKeyAccess: Enabled (consider disabling for security)"
        }

        # Connectivity verdict
        if ($saPeCount -eq 0 -and -not $hasSharedPE) {
            if ($saBypass -match 'AzureServices') {
                if ($injectedSubnetIds.Count -gt 0) {
                    Warn "Storage '$($sa.name)': no PE — agents in VNet rely on AzureServices bypass (trusted access). PE recommended."
                } else {
                    Info "Storage '$($sa.name)': no PE — relying on AzureServices bypass (trusted access)"
                }
            } else {
                Fail "Storage '$($sa.name)': no PE and no shared PE — data-plane access will fail"
            }
        }

        if ($saNetRules.ipRules -and $saNetRules.ipRules.Count -gt 0) {
            Warn "Storage '$($sa.name)': $($saNetRules.ipRules.Count) IP rule(s) — may allow public access"
        }
        if ($saNetRules.virtualNetworkRules -and $saNetRules.virtualNetworkRules.Count -gt 0) {
            Info "Storage '$($sa.name)': $($saNetRules.virtualNetworkRules.Count) VNet rule(s)"
        }
    }

    # Cosmos DB PEs + network rules
    foreach ($cdb in $cosmosAccounts) {
        $cdbPeCount = Test-PEConnections -ResourceId $cdb.id -Label "Cosmos DB '$($cdb.name)'"
        $cdbDetail = az cosmosdb show --ids $cdb.id -o json 2>$null | ConvertFrom-Json

        # Cosmos uses isVirtualNetworkFilterEnabled + virtualNetworkRules
        if ($cdbDetail.isVirtualNetworkFilterEnabled -eq $true) {
            Info "Cosmos DB '$($cdb.name)': VNet filter enabled"
            if ($cdbDetail.virtualNetworkRules -and $cdbDetail.virtualNetworkRules.Count -gt 0) {
                Info "  $($cdbDetail.virtualNetworkRules.Count) VNet rule(s) configured"
            }
        }

        if ($cdbDetail.ipRules -and $cdbDetail.ipRules.Count -gt 0) {
            Warn "Cosmos DB '$($cdb.name)': $($cdbDetail.ipRules.Count) IP rule(s) — may allow public access"
        }

        if ($cdbPeCount -eq 0) {
            if ($injectedSubnetIds.Count -gt 0) {
                Fail "Cosmos DB '$($cdb.name)': no PE — agents in VNet cannot reach Cosmos without a private endpoint"
            } else {
                Warn "Cosmos DB '$($cdb.name)': no approved PEs — data-plane access may rely on VNet rules or public access"
            }
        }
    }

    # AI Search PEs + network rules
    foreach ($ss in $searchServices) {
        $ssPeCount = Test-PEConnections -ResourceId $ss.id -Label "AI Search '$($ss.name)'"
        $ssDetail = az resource show --ids $ss.id --api-version 2025-05-01 -o json 2>$null | ConvertFrom-Json

        # Check shared private link resources on AI Search (outbound shared PEs from search to other services)
        $sharedPLResources = $ssDetail.properties.sharedPrivateLinkResources
        if ($sharedPLResources -and $sharedPLResources.Count -gt 0) {
            foreach ($spl in $sharedPLResources) {
                $splName = $spl.name
                $splStatus = $spl.properties.status
                $splTarget = $spl.properties.privateLinkResourceId
                if ($splStatus -eq 'Approved') {
                    Pass "AI Search '$($ss.name)' shared PE '$splName': $splStatus"
                } elseif ($splStatus -eq 'Pending') {
                    Fail "AI Search '$($ss.name)' shared PE '$splName': $splStatus — needs approval on target resource"
                } else {
                    Warn "AI Search '$($ss.name)' shared PE '$splName': $splStatus"
                }
            }
        }

        $ssNetRules = $ssDetail.properties.networkRuleSet
        if ($ssNetRules) {
            $ssBypass = $ssNetRules.bypass
            if ($ssNetRules.ipRules -and $ssNetRules.ipRules.Count -gt 0) {
                Warn "AI Search '$($ss.name)': $($ssNetRules.ipRules.Count) IP rule(s)"
            }
            if ($ssBypass -and $ssBypass -ne 'None') {
                Info "AI Search '$($ss.name)' bypass: $ssBypass"
            }
        }

        if ($ssPeCount -eq 0) {
            if ($injectedSubnetIds.Count -gt 0) {
                Fail "AI Search '$($ss.name)': no PE — agents in VNet cannot reach search without a private endpoint"
                Detail "Network injection is active. Create a PE for AI Search in the PE subnet and link privatelink.search.windows.net DNS zone."
            } else {
                Warn "AI Search '$($ss.name)': no approved PEs — data-plane access may fail if publicNetworkAccess is Disabled"
            }
        }
    }

    # APIM PEs
    foreach ($apim in $apimServices) {
        $apimPeCount = Test-PEConnections -ResourceId $apim.id -Label "APIM '$($apim.name)'"
        if ($apimPeCount -eq 0) {
            if ($injectedSubnetIds.Count -gt 0) {
                Fail "APIM '$($apim.name)': no PE — agents in VNet cannot reach APIM without a private endpoint"
            } else {
                Warn "APIM '$($apim.name)': no approved PEs"
            }
        }
    }

    # ACR PEs
    foreach ($acr in $containerRegistries) {
        $acrPeCount = Test-PEConnections -ResourceId $acr.id -Label "ACR '$($acr.name)'"
        if ($acrPeCount -eq 0) {
            Warn "ACR '$($acr.name)': no private endpoint — expected for private network setup"
        }
    }
    Write-Host ""

    # =============================================================================
    # 8. PROJECTS AND MANAGED IDENTITY
    # =============================================================================
    Write-Host "--- 8. Projects and Managed Identity ---"

    $projectsUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$acctName/projects?api-version=$CogApiVersion"
    $projectsResp = Invoke-ArmGet -Url $projectsUrl

    if (-not $projectsResp -or -not $projectsResp.value -or $projectsResp.value.Count -eq 0) {
        Warn "No projects found under account '$acctName'"
        Write-Host ""
        continue
    }

    $projIndex = 0
    $projTotal = $projectsResp.value.Count
    foreach ($proj in $projectsResp.value) {
        # ARM returns name as 'accountName/projectName' — extract just the project part
        $projName = ($proj.name -split '/')[-1]
        $projIndex++
        Write-Host "---------- Project $projIndex/$projTotal`: $projName ----------"
        $projState = $proj.properties.provisioningState
        $projPrincipalId = $proj.identity.principalId

        if ($projState -eq 'Succeeded') {
            Pass "Project '$projName': $projState"
        } else {
            Fail "Project '$projName': $projState"
        }

        if ($projPrincipalId) {
            Pass "Project '$projName' has system-assigned MI: $projPrincipalId"
        } else {
            Fail "Project '$projName' has no system-assigned managed identity"
        }
        Write-Host ""

        # =============================================================================
        # 9. CAPABILITY HOST STATUS
        # =============================================================================
        Write-Host "--- 9. Capability Host Status [$projName] ---"

        # Project-level capability hosts
        $projCaphostsUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$acctName/projects/$projName/capabilityHosts?api-version=$CogApiVersion"
        $projCaphosts = Invoke-ArmGet -Url $projCaphostsUrl

        if ($projCaphosts -and $projCaphosts.value -and $projCaphosts.value.Count -gt 0) {
            foreach ($ch in $projCaphosts.value) {
                $chName = $ch.name
                $chState = $ch.properties.provisioningState
                $chKind = $ch.properties.capabilityHostKind

                if ($chState -eq 'Succeeded') {
                    Pass "Project caphost '$chName' ($chKind): $chState"
                } elseif ($chState -eq 'Creating' -or $chState -eq 'Updating') {
                    Warn "Project caphost '$chName' ($chKind): $chState — still in progress"
                } else {
                    Fail "Project caphost '$chName' ($chKind): $chState"
                }

                # Check connections
                $connections = @()
                if ($ch.properties.vectorStoreConnections) { $connections += "vectorStore: $($ch.properties.vectorStoreConnections -join ',')" }
                if ($ch.properties.storageConnections) { $connections += "storage: $($ch.properties.storageConnections -join ',')" }
                if ($ch.properties.threadStorageConnections) { $connections += "threadStorage: $($ch.properties.threadStorageConnections -join ',')" }
                if ($connections.Count -gt 0) {
                    Info "  Connections: $($connections -join ' | ')"
                } else {
                    Warn "  No connections configured on caphost '$chName'"
                }
            }
        } else {
            Fail "No project-level capability hosts found for project '$projName'"
            Detail "The capability host is required for agent functionality"
            Detail "Check that RBAC was assigned before caphost creation"
        }

        # Account-level capability hosts
        $acctCaphostsUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$acctName/capabilityHosts?api-version=$CogApiVersion"
        $acctCaphosts = Invoke-ArmGet -Url $acctCaphostsUrl

        if ($acctCaphosts -and $acctCaphosts.value -and $acctCaphosts.value.Count -gt 0) {
            foreach ($ch in $acctCaphosts.value) {
                $chName = $ch.name
                $chState = $ch.properties.provisioningState
                if ($chState -eq 'Succeeded') {
                    Pass "Account caphost '$chName': $chState"
                } elseif ($chState -eq 'Creating' -or $chState -eq 'Updating') {
                    Warn "Account caphost '$chName': $chState — still in progress"
                } else {
                    Fail "Account caphost '$chName': $chState"
                }
            }
        } else {
            Info "No account-level capability hosts (deployment may use project-only pattern)"
        }
        Write-Host ""

        # =============================================================================
        # 10. PROJECT CONNECTIONS
        # =============================================================================
        Write-Host "--- 10. Project Connections [$projName] ---"

        $connUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$acctName/projects/$projName/connections?api-version=$CogApiVersion"
        $connResp = Invoke-ArmGet -Url $connUrl

        $expectedCategories = @(
            @{ Display = 'Cosmos DB'; Matches = @('AzureCosmosDBNoSQL', 'CosmosDb') },
            @{ Display = 'Storage';   Matches = @('AzureStorageAccount') },
            @{ Display = 'AI Search'; Matches = @('CognitiveSearch') }
        )
        $foundCategories = @()

        if ($connResp -and $connResp.value) {
            foreach ($conn in $connResp.value) {
                $connName = ($conn.name -split '/')[-1]
                $connCat = $conn.properties.category
                $connAuth = $conn.properties.authType
                $connTarget = $conn.properties.target
                $foundCategories += $connCat

                Pass "Connection '$connName': category=$connCat, auth=$connAuth"
                # AAD auth is expected for caphost-related connections; other categories (e.g. AppInsights) may use ApiKey
                $aadExpectedCategories = @('AzureCosmosDBNoSQL', 'CosmosDb', 'AzureStorageAccount', 'CognitiveSearch')
                if ($connAuth -ne 'AAD' -and $aadExpectedCategories -contains $connCat) {
                    Warn "  Connection uses '$connAuth' auth instead of AAD — may not work with managed identity"
                }
            }
        }

        foreach ($expected in $expectedCategories) {
            $found = $false
            foreach ($m in $expected.Matches) {
                if ($foundCategories -contains $m) { $found = $true; break }
            }
            if (-not $found) {
                Fail "Missing project connection: $($expected.Display) (expected category: $($expected.Matches -join ' or '))"
                Detail "Capability host needs this connection to function properly"
            }
        }
        Write-Host ""

        # =============================================================================
        # 11. RBAC ROLE ASSIGNMENTS
        # =============================================================================
        Write-Host "--- 11. RBAC Role Assignments [$projName] (MI: $projPrincipalId) ---"

        if ($projPrincipalId) {
            # Check role assignments for project MI across the RG scope
            $raJson = az role assignment list --assignee $projPrincipalId --resource-group $ResourceGroup --include-inherited -o json 2>$null | ConvertFrom-Json

            # Also check cross-RG role assignments (for BYO resources)
            $raAllJson = az role assignment list --assignee $projPrincipalId --all -o json 2>$null | ConvertFrom-Json

            $allRolesFound = @()
            if ($raJson) { $allRolesFound += $raJson }
            if ($raAllJson) { $allRolesFound += $raAllJson }
            $allRolesFound = $allRolesFound | Sort-Object -Property id -Unique

            # Expected roles
            $expectedRoles = @{
                'Cosmos DB Operator'          = @{ scope = 'Cosmos DB'; required = $true;  preCaphost = $true }
                'Storage Blob Data Contributor' = @{ scope = 'Storage'; required = $true;  preCaphost = $true }
                'Search Index Data Contributor' = @{ scope = 'AI Search'; required = $true; preCaphost = $true }
                'Search Service Contributor'    = @{ scope = 'AI Search'; required = $true; preCaphost = $true }
                'Storage Blob Data Owner'       = @{ scope = 'Storage'; required = $true;  preCaphost = $false }
            }

            foreach ($roleName in $expectedRoles.Keys) {
                $found = $allRolesFound | Where-Object { $_.roleDefinitionName -eq $roleName }
                $meta = $expectedRoles[$roleName]
                if ($found) {
                    $timing = if ($meta.preCaphost) { "pre-caphost" } else { "post-caphost" }
                    Pass "Role '$roleName' assigned on $($meta.scope) ($timing)"

                    # Check ABAC condition on Storage Blob Data Owner
                    if ($roleName -eq 'Storage Blob Data Owner') {
                        $hasCondition = $found | Where-Object { $_.condition }
                        if ($hasCondition) {
                            Pass "  Storage Blob Data Owner has ABAC condition (scoped to agent containers)"
                        } else {
                            Warn "  Storage Blob Data Owner has no ABAC condition — broader than needed"
                        }
                    }
                } else {
                    Fail "Role '$roleName' NOT found for project MI on $($meta.scope)"
                    if ($meta.preCaphost) {
                        Detail "This role must be assigned BEFORE capability host creation"
                    } else {
                        Detail "This role must be assigned AFTER capability host creation"
                    }
                }
            }

            # Check Cosmos DB SQL data-plane role
            foreach ($cdb in $cosmosAccounts) {
                $cdbRG = ($cdb.id -split '/')[4]
                $cosmosRoles = az cosmosdb sql role assignment list --account-name $cdb.name --resource-group $cdbRG -o json 2>$null | ConvertFrom-Json
                if ($cosmosRoles) {
                    $dataContrib = $cosmosRoles | Where-Object {
                        $_.principalId -eq $projPrincipalId -and
                        $_.roleDefinitionId -match '00000000-0000-0000-0000-000000000002'
                    }
                    if ($dataContrib) {
                        Pass "Cosmos DB SQL Built-in Data Contributor role assigned to project MI"
                    } else {
                        Fail "Cosmos DB SQL Built-in Data Contributor role NOT assigned to project MI"
                        Detail "This data-plane role must be assigned AFTER capability host creation"
                    }
                }
            }
        } else {
            Warn "Skipping RBAC checks — no project MI principal ID"
        }
        Write-Host ""
        Write-Host "---------- End Project: $projName ----------"
        Write-Host ""

    }  # end foreach project

    # =============================================================================
    # 12. RESOURCE PROVISIONING STATE
    # =============================================================================
    Write-Host "--- 12. Resource Provisioning State ---"

    $acctState = $acctDetail.properties.provisioningState
    if ($acctState -eq 'Succeeded') {
        Pass "AI Services account: $acctState"
    } else {
        Fail "AI Services account: $acctState"
    }

    $depTypes = @(
        'Microsoft.Storage/storageAccounts',
        'Microsoft.DocumentDB/databaseAccounts',
        'Microsoft.Search/searchServices',
        'Microsoft.ContainerRegistry/registries',
        'Microsoft.ApiManagement/service'
    )
    foreach ($depType in $depTypes) {
        $typeName = $depType.Split('/')[-1]
        # Use the merged typed arrays (includes BYO resources from other RGs)
        $resources = switch ($depType) {
            'Microsoft.Storage/storageAccounts' { $storageAccounts }
            'Microsoft.DocumentDB/databaseAccounts' { $cosmosAccounts }
            'Microsoft.Search/searchServices' { $searchServices }
            'Microsoft.ContainerRegistry/registries' { $containerRegistries }
            'Microsoft.ApiManagement/service' { $apimServices }
        }
        foreach ($res in $resources) {
            $resDetail = az resource show --ids $res.id -o json 2>$null | ConvertFrom-Json
            $state = $resDetail.properties.provisioningState
            if (-not $state) { $state = "Unknown" }
            $resRG = ($res.id -split '/')[4]
            $rgLabel = if ($resRG -ne $ResourceGroup) { " (BYO RG: $resRG)" } else { "" }
            if ($state -eq 'Succeeded') {
                Pass "$typeName '$($res.name)'${rgLabel}: $state"
            } else {
                Fail "$typeName '$($res.name)'${rgLabel}: $state"
            }
        }
        if ($resources.Count -eq 0) {
            Info "No $typeName found in RG or via BYO discovery"
        }
    }
    Write-Host ""

    # =============================================================================
    # 13. PUBLIC NETWORK ACCESS + AI SERVICES ACLS (Lockdown Audit)
    # =============================================================================
    Write-Host "--- 13. Public Network Access and ACL Lockdown ---"

    $publicAccess = $acctDetail.properties.publicNetworkAccess
    if ($publicAccess -eq 'Disabled') {
        Pass "AI Services publicNetworkAccess: Disabled"
    } elseif ($publicAccess -eq 'Enabled') {
        Warn "AI Services publicNetworkAccess: Enabled (expected Disabled for private network setup)"
    } else {
        Info "AI Services publicNetworkAccess: $publicAccess"
    }

    # AI Services network ACLs
    $networkAcls = $acctDetail.properties.networkAcls
    if ($networkAcls) {
        $defaultAction = $networkAcls.defaultAction
        $bypass = $networkAcls.bypass

        if ($defaultAction -eq 'Deny') {
            Pass "AI Services ACL defaultAction: Deny"
        } else {
            Warn "AI Services ACL defaultAction: $defaultAction (expected Deny for private setup)"
        }

        if ($bypass -match 'AzureServices') {
            Pass "AI Services ACL bypass includes AzureServices"
        } else {
            Warn "AI Services ACL bypass does not include AzureServices — trusted Azure services may be blocked"
        }

        if ($networkAcls.ipRules -and $networkAcls.ipRules.Count -gt 0) {
            Warn "AI Services ACL has $($networkAcls.ipRules.Count) IP rule(s) — may allow public access"
        }
        if ($networkAcls.virtualNetworkRules -and $networkAcls.virtualNetworkRules.Count -gt 0) {
            Info "AI Services ACL has $($networkAcls.virtualNetworkRules.Count) VNet rule(s)"
        }
    } else {
        Warn "No network ACLs configured on AI Services account"
    }

    # Check Storage public access
    foreach ($sa in $storageAccounts) {
        $saDetail = az storage account show --ids $sa.id -o json 2>$null | ConvertFrom-Json
        $saPNA = $saDetail.publicNetworkAccess
        if ($saPNA -eq 'Disabled') {
            Pass "Storage '$($sa.name)' publicNetworkAccess: Disabled"
        } else {
            Warn "Storage '$($sa.name)' publicNetworkAccess: $saPNA (expected Disabled)"
        }
    }

    # Check Cosmos DB public access + auth
    foreach ($cdb in $cosmosAccounts) {
        $cdbDetail = az cosmosdb show --ids $cdb.id -o json 2>$null | ConvertFrom-Json
        $cdbPNA = $cdbDetail.publicNetworkAccess
        if ($cdbPNA -eq 'Disabled') {
            Pass "Cosmos DB '$($cdb.name)' publicNetworkAccess: Disabled"
        } else {
            Warn "Cosmos DB '$($cdb.name)' publicNetworkAccess: $cdbPNA (expected Disabled)"
        }
        if ($cdbDetail.disableLocalAuth -eq $true) {
            Pass "Cosmos DB '$($cdb.name)' disableLocalAuth: true (Entra-only)"
        } else {
            Warn "Cosmos DB '$($cdb.name)' disableLocalAuth: false — key-based auth enabled (expected true for private setups)"
        }
    }

    # Check AI Search public access + auth
    foreach ($ss in $searchServices) {
        $ssDetail = az resource show --ids $ss.id --api-version 2025-05-01 -o json 2>$null | ConvertFrom-Json
        $ssPNA = $ssDetail.properties.publicNetworkAccess ?? $ssDetail.properties.publicInternetAccess
        if ($ssPNA -eq 'disabled' -or $ssPNA -eq 'Disabled') {
            Pass "AI Search '$($ss.name)' publicNetworkAccess: Disabled"
        } elseif (-not $ssPNA) {
            Warn "AI Search '$($ss.name)' publicNetworkAccess: unknown (API call may have failed)"
        } else {
            Warn "AI Search '$($ss.name)' publicNetworkAccess: $ssPNA (expected Disabled)"
        }
        # Auth check
        $ssLocalAuth = $ssDetail.properties.disableLocalAuth
        $ssAuthOptions = $ssDetail.properties.authOptions
        if ($ssLocalAuth -eq $true) {
            Pass "AI Search '$($ss.name)' disableLocalAuth: true (Entra-only)"
        } elseif ($ssAuthOptions -and $ssAuthOptions.aadOrApiKey) {
            Pass "AI Search '$($ss.name)' authOptions: aadOrApiKey (AAD accepted alongside API key)"
        } elseif ($ssLocalAuth -eq $false -and -not $ssAuthOptions) {
            Warn "AI Search '$($ss.name)' disableLocalAuth: false with no aadOrApiKey — API-key only, AAD tokens rejected"
        } else {
            Info "AI Search '$($ss.name)' auth: disableLocalAuth=$ssLocalAuth"
        }
    }

    # Check ACR public access
    foreach ($acr in $containerRegistries) {
        $acrDetail = az acr show --ids $acr.id -o json 2>$null | ConvertFrom-Json
        $acrPNA = $acrDetail.publicNetworkAccess
        if ($acrPNA -eq 'Disabled') {
            Pass "ACR '$($acr.name)' publicNetworkAccess: Disabled"
        } else {
            Info "ACR '$($acr.name)' publicNetworkAccess: $acrPNA (developer access mode — verify IP allowlist is configured)"
        }
    }

    # Check APIM public access
    foreach ($apim in $apimServices) {
        $apimDetail = az resource show --ids $apim.id -o json 2>$null | ConvertFrom-Json
        $apimPNA = $apimDetail.properties.publicNetworkAccess
        if ($apimPNA -eq 'Disabled') {
            Pass "APIM '$($apim.name)' publicNetworkAccess: Disabled"
        } else {
            Warn "APIM '$($apim.name)' publicNetworkAccess: $apimPNA (expected Disabled for private setup)"
        }
    }

    # AI Services local auth
    $acctLocalAuth = $acctDetail.properties.disableLocalAuth
    if ($acctLocalAuth -eq $true) {
        Pass "AI Services '$acctName' disableLocalAuth: true (Entra-only)"
    } else {
        Info "AI Services '$acctName' disableLocalAuth: false (API keys enabled — expected for agent setups)"
    }

    Info "(Storage, Cosmos DB, AI Search, ACR, and APIM network rules are checked in section 7 — Private Endpoints)"
    Write-Host ""

    # =============================================================================
    # 14. MODEL DEPLOYMENT
    # =============================================================================
    Write-Host "--- 14. Model Deployments ---"

    $deploymentsJson = az cognitiveservices account deployment list --name $acctName --resource-group $ResourceGroup -o json 2>$null | ConvertFrom-Json
    if ($deploymentsJson -and $deploymentsJson.Count -gt 0) {
        foreach ($dep in $deploymentsJson) {
            $depName = $dep.name
            $depState = $dep.properties.provisioningState
            $modelName = $dep.properties.model.name
            $modelVersion = $dep.properties.model.version
            $sku = $dep.sku.name
            $capacity = $dep.sku.capacity

            if ($depState -eq 'Succeeded') {
                Pass "Model '$depName' ($modelName v$modelVersion, $sku, ${capacity} TPM): $depState"
            } else {
                Fail "Model '$depName' ($modelName v$modelVersion): $depState"
            }
        }
    } else {
        Warn "No model deployments found on account '$acctName'"
    }
    Write-Host ""

}  # end foreach account

# =============================================================================
# 15. AZURE POLICY COMPLIANCE (summary only — Deny policies can block redeployments)
# =============================================================================
Write-Host "--- 15. Azure Policy Compliance ---"

$policyStates = az policy state list --resource-group $ResourceGroup --filter "complianceState eq 'NonCompliant'" -o json 2>$null | ConvertFrom-Json

if ($policyStates -and $policyStates.Count -gt 0) {
    $denyPolicies = @($policyStates | Where-Object { $_.policyDefinitionAction -in 'deny', 'Deny' })
    $otherPolicies = @($policyStates | Where-Object { $_.policyDefinitionAction -notin 'deny', 'Deny' })

    if ($denyPolicies.Count -gt 0) {
        $denyGrouped = $denyPolicies | Group-Object -Property policyAssignmentName
        Warn "$($denyPolicies.Count) Deny policy evaluation(s) across $($denyGrouped.Count) assignment(s) — may block redeployment"
        foreach ($g in $denyGrouped) {
            $resources = ($g.Group | ForEach-Object { ($_.resourceId -split '/')[-1] } | Select-Object -Unique) -join ', '
            Detail "  Assignment '$($g.Name)': $resources"
        }
    } else {
        Pass "No Deny policies — redeployments will not be blocked by policy"
    }

    if ($otherPolicies.Count -gt 0) {
        Info "$($otherPolicies.Count) non-blocking policy evaluation(s) (Audit/DINE) — informational only"
    }
} else {
    Pass "No non-compliant Azure Policy evaluations in resource group"
}
Write-Host ""

# =============================================================================
# SUMMARY
# =============================================================================
Write-Host ""
Write-Host "========================================"
Write-Host "Diagnostic Summary"
Write-Host "========================================"
Write-Host "  Passed:   $($script:PassCount)" -ForegroundColor Green
Write-Host "  Failed:   $($script:FailCount)" -ForegroundColor $(if ($script:FailCount -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Warnings: $($script:WarnCount)" -ForegroundColor $(if ($script:WarnCount -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "========================================"

if ($script:FailCount -gt 0) {
    Write-Host ""
    Write-Host "Common remediation steps:" -ForegroundColor Yellow
    Write-Host "  - Net injection missing: Network injection must be configured on the AI Services account (section 2)"
    Write-Host "  - SAL missing:           Agent subnet has no SAL. Caphost may not have provisioned (section 3)"
    Write-Host "  - NSG blocking:          Ensure outbound 443 to AzureCloud and inbound from VNet on PE/MCP subnets (section 4)"
    Write-Host "  - Missing DNS link:      'az network private-dns link vnet create' to link zone to your VNet (section 5)"
    Write-Host "  - Custom DNS:            Add conditional forwarders to 168.63.129.16 for all privatelink.* zones (section 6)"
    Write-Host "  - PE 'Pending':          Approve via portal or 'az network private-endpoint-connection approve' (section 7)"
    Write-Host "  - Caphost 'Failed':      Delete caphost, use a NEW VNet/subnet, and re-deploy (section 9)"
    Write-Host "  - Missing connection:    Re-deploy or manually create project connections for Cosmos/Storage/Search (section 10)"
    Write-Host "  - Missing RBAC:          Pre-caphost roles must exist before caphost creation. Re-deploy or assign manually (section 11)"
    Write-Host "  - Policy 'Deny':         Review Azure Policy assignments. Exempt or adjust policies blocking deployment (section 15)"
    Write-Host ""
    Write-Host "Docs: https://learn.microsoft.com/azure/ai-foundry/how-to/configure-private-link"
    Write-Host "      https://learn.microsoft.com/azure/ai-services/agents/how-to/virtual-networks"
    exit 1
} else {
    Write-Host ""
    Write-Host "All checks passed. If agents still fail, test from within the VNet (VPN/Bastion)." -ForegroundColor Green
    exit 0
}
