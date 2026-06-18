<#
.SYNOPSIS
    Foundry Private Network Cleanup Script

.DESCRIPTION
    Safely tears down Foundry deployments with VNet injection.
    Auto-discovers all resources — no need to know account/project names.

.PARAMETER SubscriptionId
    Azure subscription ID

.PARAMETER ResourceGroup
    Resource group containing the AI Foundry account, project, and VNet

.PARAMETER AccountName
    Optional. Limit cleanup to a specific AI Services account.
    When omitted, all AIServices accounts in the RG are discovered.

.PARAMETER DryRun
    Show what would be cleaned up without taking action

.PARAMETER SkipSalWait
    Don't wait for serviceAssociationLink removal (faster but risky)

.PARAMETER DeleteRG
    Delete the resource group after cleanup

.EXAMPLE
    .\cleanup.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg" -DryRun
    .\cleanup.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg" -AccountName "my-account"
    .\cleanup.ps1 -SubscriptionId "xxx" -ResourceGroup "my-rg" -DeleteRG
#>

param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][string]$ResourceGroup,
    [string]$AccountName,
    [switch]$DryRun,
    [switch]$SkipSalWait,
    [switch]$DeleteRG
)

if ($AccountName -and $DeleteRG) {
    Write-Host "[FAIL] -DeleteRG cannot be used with -AccountName. Account-scoped cleanup must not delete the whole resource group." -ForegroundColor Red
    exit 1
}

$ErrorActionPreference = "Continue"
# ARM API version for CognitiveServices capabilityHosts — update when this API reaches GA
$ApiVersion = "2025-04-01-preview"
$script:Errors = 0

# ---- Logging ----
function Log   { param([string]$Msg) Write-Host "[INFO] $Msg" -ForegroundColor Cyan }
function Pass  { param([string]$Msg) Write-Host "[DONE] $Msg" -ForegroundColor Green }
function Warn  { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow }
function Fail  { param([string]$Msg) Write-Host "[FAIL] $Msg" -ForegroundColor Red; $script:Errors++ }
function Dry   { param([string]$Msg) Write-Host "[DRY-RUN] Would: $Msg" -ForegroundColor Yellow }

function Get-AzToken {
    az account get-access-token --query accessToken -o tsv 2>$null
}

# ---- Caphost deletion with full error handling ----
function Remove-CapabilityHost {
    param(
        [string]$ResourcePath,   # e.g. "accounts/myaccount" or "accounts/myaccount/projects/myproject"
        [string]$CaphostName,
        [string]$DisplayName
    )

    $apiUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/$ResourcePath/capabilityHosts/${CaphostName}?api-version=$ApiVersion"

    if ($DryRun) {
        Dry "Delete caphost: $DisplayName ($CaphostName)"
        return $true
    }

    Log "Deleting $DisplayName capability host: $CaphostName"
    $token = Get-AzToken

    try {
        $headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
        $response = Invoke-WebRequest -Uri $apiUrl -Method Delete -Headers $headers -ErrorAction Stop

        if ($response.StatusCode -eq 200) {
            Pass "$DisplayName caphost deleted (synchronous)"
            return $true
        }

        if ($response.StatusCode -eq 202) {
            # Async — extract operation URL and poll
            $operationUrl = $response.Headers["Azure-AsyncOperation"]
            if (-not $operationUrl) {
                $operationUrl = $response.Headers["azure-asyncoperation"]
            }
            if (-not $operationUrl) {
                Warn "No async operation URL returned. Assuming deletion in progress."
                return $true
            }
            # Handle array header values
            if ($operationUrl -is [array]) { $operationUrl = $operationUrl[0] }

            Log "Polling deletion status..."
            $status = "Deleting"
            $pollCount = 0
            $maxPolls = 60  # 30 min max (account caphosts can take 15-20 min)

            while ($status -eq "Deleting" -or $status -eq "InProgress" -or $status -eq "Creating" -or $status -eq "Running") {
                Start-Sleep -Seconds 30
                $pollCount++
                if ($pollCount -ge $maxPolls) {
                    Fail "Timeout polling caphost deletion after 30 minutes"
                    return $false
                }
                $token = Get-AzToken
                $pollHeaders = @{ Authorization = "Bearer $token" }
                try {
                    $pollResponse = Invoke-RestMethod -Uri $operationUrl -Headers $pollHeaders -ErrorAction Stop
                    if ($pollResponse.error.code -eq "TransientError") {
                        Warn "Transient error, retrying..."
                        continue
                    }
                    $status = $pollResponse.status
                    Log "  Status: $status ($pollCount/$maxPolls)"
                } catch {
                    Warn "Poll error: $($_.Exception.Message). Retrying..."
                }
            }

            if ($status -eq "Succeeded") {
                Pass "$DisplayName caphost deleted successfully"
                return $true
            } elseif ($status -eq "Failed" -or $status -eq "Canceled") {
                Fail "$DisplayName caphost deletion failed with status: $status"
                return $false
            } else {
                Warn "$DisplayName caphost deletion returned status: $status. The DELETE was accepted — backend cleanup may still be in progress."
                Warn "The SAL wait step will verify whether cleanup completed."
                return $true
            }
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errBody = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        switch ($statusCode) {
            404 {
                Pass "$DisplayName caphost not found (already deleted)"
                return $true
            }
            409 {
                Fail "$DisplayName caphost returned 409 Conflict. It may be in a failed state."
                Fail "Error: $errBody"
                return $false
            }
            default {
                Fail "$DisplayName caphost deletion failed (HTTP $statusCode)"
                Fail "Error: $errBody"
                return $false
            }
        }
    }
}

# ---- SAL wait ----
function Wait-ForSalCleanup {
    param(
        [string]$VnetRg,
        [string]$VnetName,
        [string]$SubnetName
    )

    if ($DryRun) {
        Dry "Wait for SAL removal on $VnetName/$SubnetName"
        return $true
    }

    if ($SkipSalWait) {
        Warn "Skipping SAL wait for $SubnetName (--SkipSalWait)"
        return $true
    }

    $maxWait = 1200  # 20 min
    $elapsed = 0

    Log "Waiting for serviceAssociationLink cleanup on $SubnetName (up to 20 min)..."
    Log "SAL is held by the platform's managed Container Apps environment — cleanup happens asynchronously after caphost deletion."

    while ($elapsed -lt $maxWait) {
        $subnetInfo = az network vnet subnet show `
            --resource-group $VnetRg `
            --vnet-name $VnetName `
            --name $SubnetName `
            --query "{salCount:length(serviceAssociationLinks || ``[]``), salType:(serviceAssociationLinks[0].linkedResourceType || serviceAssociationLinks[0].properties.linkedResourceType), salState:(serviceAssociationLinks[0].provisioningState || serviceAssociationLinks[0].properties.provisioningState)}" `
            -o json 2>$null | ConvertFrom-Json

        $salCount = $subnetInfo.salCount
        if ($salCount -eq 0) {
            Pass "serviceAssociationLink removed from $SubnetName"
            return $true
        }

        if ($elapsed -eq 0) {
            Log "  SAL type: $($subnetInfo.salType) | state: $($subnetInfo.salState)"
        }

        Log "  Still linked ($salCount SALs, state: $($subnetInfo.salState)). Elapsed: ${elapsed}s / ${maxWait}s"
        Start-Sleep -Seconds 30
        $elapsed += 30
    }

    Warn "SAL still present on $SubnetName after 20 min. Backend cleanup can take up to 24 hours."
    Warn "Diagnostics to run manually:"
    Warn "  1. Check SAL:  az network vnet subnet show --resource-group $VnetRg --vnet-name $VnetName --name $SubnetName --query serviceAssociationLinks"
    Warn "  2. Check caphosts still exist:  az rest --method GET --url `"https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/<account>/capabilityHosts?api-version=$ApiVersion`""
    Warn "If still present after 24 hours, file a support ticket."
    return $false
}

# =============================================================================
# STEP 0: Discovery
# =============================================================================
Write-Host "========================================"
Write-Host "Foundry Private Network Cleanup"
Write-Host "========================================"
Write-Host "Subscription: $SubscriptionId"
Write-Host "Resource Group: $ResourceGroup"
if ($AccountName) {
    Write-Host "Account filter: $AccountName"
}
Write-Host "Dry Run: $DryRun"
Write-Host ""

# Verify login and active subscription (never switch the user's active subscription)
$azAccount = az account show -o json 2>$null | ConvertFrom-Json
if (-not $azAccount) {
    Fail "Not logged in to Azure CLI. Run: az login"
    return
}

# Verify subscription access without switching the active context
$subCheck = az account show --subscription $SubscriptionId --query "id" -o tsv 2>$null
if (-not $subCheck) {
    Fail "Cannot access subscription $SubscriptionId. Verify the ID and your access."
    return
}

# Ensure the CLI is already pointed at the right subscription
$activeSubId = ($azAccount.id).Trim()
if ($activeSubId -ne $SubscriptionId) {
    Fail "Active subscription ($activeSubId) does not match requested ($SubscriptionId)."
    Fail "Run: az account set --subscription $SubscriptionId"
    return
}
Pass "Subscription verified: $($azAccount.name)"

# Discover AI Foundry accounts
Log "Discovering AI Foundry accounts..."
if ($AccountName) {
    # Verify the specific account exists and is AIServices
    $kind = az cognitiveservices account show --name $AccountName --resource-group $ResourceGroup `
        --query "kind" -o tsv 2>$null
    if ($kind -eq "AIServices") {
        $accounts = @($AccountName)
    } else {
        Fail "Account '$AccountName' not found or is not an AIServices account in $ResourceGroup"
        return
    }
} else {
    $accounts = @(az cognitiveservices account list --resource-group $ResourceGroup `
        --query "[?kind=='AIServices'].name" -o tsv 2>$null) | Where-Object { $_ }
}

if ($accounts.Count -eq 0) {
    Warn "No AI Foundry accounts found in $ResourceGroup"
} else {
    Log "Found accounts: $($accounts -join ', ')"
}

# Discover caphosts per account (subnet tracking is derived from caphost properties)
Log "Discovering capability hosts..."
$token = Get-AzToken
$headers = @{ Authorization = "Bearer $token" }

$projectCaphosts = @()  # [PSCustomObject]{Account, Project, Caphost}
$accountCaphosts = @()  # [PSCustomObject]{Account, Caphost}
$accountProjects = @()  # [PSCustomObject]{Account, Project}
$caphostSubnets = @{}   # Deduplicated: subnetResourceId -> [PSCustomObject]{VnetRg, Vnet, Subnet}

foreach ($account in $accounts) {
    # Account-level caphosts
    try {
        $accCH = Invoke-RestMethod -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$account/capabilityHosts?api-version=$ApiVersion" -Headers $headers -ErrorAction Stop
        foreach ($ch in $accCH.value) {
            $accountCaphosts += [PSCustomObject]@{ Account = $account; Caphost = $ch.name }
            Log "  Account caphost: $account -> $($ch.name)"
            # Track the subnet this caphost is linked to
            $subnetId = $ch.properties.customerSubnet
            if ($subnetId -and -not $caphostSubnets.ContainsKey($subnetId)) {
                if ($subnetId -match '/subscriptions/[^/]+/resourceGroups/([^/]+)/providers/Microsoft.Network/virtualNetworks/([^/]+)/subnets/([^/]+)') {
                    $caphostSubnets[$subnetId] = [PSCustomObject]@{ VnetRg = $Matches[1]; Vnet = $Matches[2]; Subnet = $Matches[3] }
                    Log "  Caphost subnet: $($Matches[2])/$($Matches[3])"
                }
            }
        }
    } catch { }

    # Discover projects
    try {
        $projects = Invoke-RestMethod -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$account/projects?api-version=$ApiVersion" -Headers $headers -ErrorAction Stop
        foreach ($proj in $projects.value) {
            # API returns "account/project" format — extract just the project name
            $projName = if ($proj.name -match '/') { ($proj.name -split '/')[-1] } else { $proj.name }
            $accountProjects += [PSCustomObject]@{ Account = $account; Project = $projName }
            Log "  Project: $account/$projName"
            # Project-level caphosts
            try {
                $projCH = Invoke-RestMethod -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$account/projects/$projName/capabilityHosts?api-version=$ApiVersion" -Headers $headers -ErrorAction Stop
                foreach ($ch in $projCH.value) {
                    $projectCaphosts += [PSCustomObject]@{ Account = $account; Project = $projName; Caphost = $ch.name }
                    Log "  Project caphost: $account/$projName -> $($ch.name)"
                    # Track the subnet this caphost is linked to
                    $subnetId = $ch.properties.customerSubnet
                    if ($subnetId -and -not $caphostSubnets.ContainsKey($subnetId)) {
                        if ($subnetId -match '/subscriptions/[^/]+/resourceGroups/([^/]+)/providers/Microsoft.Network/virtualNetworks/([^/]+)/subnets/([^/]+)') {
                            $caphostSubnets[$subnetId] = [PSCustomObject]@{ VnetRg = $Matches[1]; Vnet = $Matches[2]; Subnet = $Matches[3] }
                            Log "  Caphost subnet: $($Matches[2])/$($Matches[3])"
                        }
                    }
                }
            } catch { }
        }
    } catch { }
}


if ($caphostSubnets.Count -eq 0 -and $accounts.Count -gt 0) {
    Warn "No caphost-linked subnet found. Skipping SAL wait."
}

# Summary
Write-Host ""
Write-Host "========================================"
Write-Host "Discovery Summary"
Write-Host "========================================"
Write-Host "  Accounts: $($accounts.Count)"
Write-Host "  Project caphosts: $($projectCaphosts.Count)"
Write-Host "  Account caphosts: $($accountCaphosts.Count)"
Write-Host "  Projects: $($accountProjects.Count)"
Write-Host "  Caphost subnets to monitor: $($caphostSubnets.Count)"
Write-Host "========================================"
Write-Host ""

# Confirmation prompt (skip with -DryRun)
$totalItems = $accounts.Count + $projectCaphosts.Count + $accountCaphosts.Count + $caphostSubnets.Count
if ($totalItems -eq 0) {
    Warn "Nothing to clean up."
    return
}

if (-not $DryRun) {
    Write-Host "This will DELETE the resources listed above. This action cannot be undone." -ForegroundColor Yellow
    $confirm = Read-Host "Continue? [y/N]"
    if ($confirm -notmatch '^[Yy]') {
        Write-Host "Aborted."
        return
    }
}

# =============================================================================
# STEP 1: Delete Project Capability Hosts
# =============================================================================
Write-Host "=== Step 1: Delete Project Capability Hosts ==="
if ($projectCaphosts.Count -eq 0) {
    Log "No project capability hosts to delete"
} else {
    foreach ($pc in $projectCaphosts) {
        $result = Remove-CapabilityHost -ResourcePath "accounts/$($pc.Account)/projects/$($pc.Project)" `
            -CaphostName $pc.Caphost -DisplayName "Project $($pc.Project)"
        if (-not $result) {
            Fail "Cannot proceed — project caphost deletion failed. Fix the issue above and re-run."
            exit 1
        }
    }
}

# =============================================================================
# STEP 2: Delete Account Capability Hosts
# =============================================================================
Write-Host ""
Write-Host "=== Step 2: Delete Account Capability Hosts ==="
if ($accountCaphosts.Count -eq 0) {
    Log "No account capability hosts to delete"
} else {
    foreach ($ac in $accountCaphosts) {
        $result = Remove-CapabilityHost -ResourcePath "accounts/$($ac.Account)" `
            -CaphostName $ac.Caphost -DisplayName "Account $($ac.Account)"
        if (-not $result) {
            Fail "Cannot proceed — account caphost deletion failed. Fix the issue above and re-run."
            exit 1
        }
    }
}

# =============================================================================
# STEP 3: Delete Projects and Purge AI Accounts
# =============================================================================
Write-Host ""
Write-Host "=== Step 3: Delete Projects and Purge AI Accounts ==="

# 3a: Delete projects first (accounts cannot be deleted while nested projects exist)
if ($accountProjects.Count -gt 0) {
    foreach ($ap in $accountProjects) {
        if ($DryRun) {
            Dry "Delete project: $($ap.Account)/$($ap.Project)"
            continue
        }
        Log "Deleting project: $($ap.Account)/$($ap.Project)"
        $token = Get-AzToken
        $projUrl = "https://management.azure.com/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$($ap.Account)/projects/$($ap.Project)?api-version=$ApiVersion"
        try {
            Invoke-WebRequest -Uri $projUrl -Method Delete -Headers @{ Authorization = "Bearer $token" } -ErrorAction Stop | Out-Null
            Pass "Project $($ap.Project) deleted"
        } catch {
            if ($_.Exception.Response.StatusCode.value__ -eq 404) {
                Pass "Project $($ap.Project) not found (already deleted)"
            } else {
                $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
                Fail "Failed to delete project $($ap.Project): $errMsg"
            }
        }
    }
}

# 3b: Delete and purge accounts
foreach ($account in $accounts) {
    if ($DryRun) {
        Dry "Delete + purge account: $account"
        continue
    }

    $location = az cognitiveservices account show --name $account --resource-group $ResourceGroup `
        --query "location" -o tsv 2>$null

    if ([string]::IsNullOrEmpty($location)) {
        Warn "Account $account not found (already deleted)"
    } else {
        Log "Deleting account: $account"
        $delOut = az cognitiveservices account delete --name $account --resource-group $ResourceGroup 2>&1
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to delete account ${account}: $delOut"
            continue
        }
        Log "Purging account: $account (location: $location)"
        $purgeOut = az cognitiveservices account purge --name $account --resource-group $ResourceGroup --location $location 2>&1
        if ($LASTEXITCODE -ne 0) {
            Fail "Failed to purge account ${account}: $purgeOut"
            continue
        }
        Pass "Account $account deleted and purged"
    }
}

# Check for soft-deleted accounts
Log "Checking for soft-deleted accounts in resource group (residue cleanup)..."
$deletedAccounts = az cognitiveservices account list-deleted `
    --query "[?contains(id, '/resourceGroups/$ResourceGroup/')].{name:name, location:location}" -o json 2>$null | ConvertFrom-Json

if ($deletedAccounts -and $deletedAccounts.Count -gt 0) {
    foreach ($da in $deletedAccounts) {
        if ($DryRun) {
            Dry "Purge soft-deleted account: $($da.name)"
        } else {
            Log "Purging soft-deleted account: $($da.name)"
            az cognitiveservices account purge --name $da.name --resource-group $ResourceGroup --location $da.location 2>$null
            Pass "Purged: $($da.name)"
        }
    }
} else {
    Pass "No soft-deleted accounts found"
}

# =============================================================================
# STEP 4: Wait for SAL cleanup
# =============================================================================
Write-Host ""
Write-Host "=== Step 4: Wait for Subnet Link Cleanup ==="
if ($caphostSubnets.Count -eq 0) {
    Log "No caphost subnets to monitor — skipping wait"
} else {
    foreach ($entry in $caphostSubnets.Values) {
        $salClean = Wait-ForSalCleanup -VnetRg $entry.VnetRg -VnetName $entry.Vnet -SubnetName $entry.Subnet
        if (-not $salClean) {
            Fail "SAL cleanup timed out on $($entry.Vnet)/$($entry.Subnet) — subnet is still blocked"
        }
    }
}

# =============================================================================
# STEP 5: Delete Resource Group (optional)
# =============================================================================
Write-Host ""
Write-Host "=== Step 5: Resource Group ==="
if ($DeleteRG) {
    if ($DryRun) {
        Dry "Delete resource group: $ResourceGroup"
    } else {
        Log "Deleting resource group: $ResourceGroup"
        az group delete --name $ResourceGroup --yes --no-wait 2>$null
        Pass "Resource group deletion initiated (async)"
    }
} else {
    Write-Host "To delete the resource group:"
    Write-Host "  az group delete --name $ResourceGroup --yes"
}

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
Write-Host "========================================"
if ($DryRun) {
    Write-Host "DRY RUN complete. No changes were made." -ForegroundColor Yellow
} elseif ($script:Errors -eq 0) {
    Write-Host "Cleanup completed successfully." -ForegroundColor Green
} else {
    Write-Host "Cleanup completed with $($script:Errors) error(s). Review output above." -ForegroundColor Red
}
Write-Host "========================================"
return
