#!/usr/bin/env pwsh
# ─────────────────────────────────────────────────────────────────────
# migrate.ps1 — Simplest possible v1→v2 agent migration
#
# Just provide your Azure Resource ID (from portal) and everything
# else is handled: login, tenant discovery, tokens, endpoint construction.
# ─────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"

$Green  = "`e[32m"
$Blue   = "`e[34m"
$Yellow = "`e[33m"
$Red    = "`e[31m"
$Cyan   = "`e[36m"
$Bold   = "`e[1m"
$Reset  = "`e[0m"

$resourceId       = $null
$endpoint         = $null
$sourceResourceId = $null
$sourceEndpoint   = $null
$listMode         = $false
$passthrough      = @()

for ($i = 0; $i -lt $args.Count; $i++) {
	switch ($args[$i]) {
		"--resource-id"        { $resourceId       = $args[++$i] }
		"--endpoint"           { $endpoint         = $args[++$i] }
		"--source-resource-id" { $sourceResourceId = $args[++$i] }
		"--source-endpoint"    { $sourceEndpoint   = $args[++$i] }
		"--list"               { $listMode = $true; $passthrough += "--list" }
		default                { $passthrough += $args[$i] }
	}
}

Write-Host ""
Write-Host "${Blue}${Bold}===============================================================${Reset}"
Write-Host "${Blue}${Bold}  v1 to v2 Agent Migration (simplified)${Reset}"
Write-Host "${Blue}${Bold}===============================================================${Reset}"
Write-Host ""

if (-not $resourceId) {
	Write-Host "${Red}ERROR: Missing required ${Bold}--resource-id${Reset}"
	Write-Host ""
	Write-Host "${Yellow}Usage:${Reset}"
	Write-Host "  .\migrate.ps1 --resource-id <ARM_RESOURCE_ID> [options]"
	exit 1
}

function Parse-ResourceId {
	param([string]$Id)
	$pattern = "^/subscriptions/([^/]+)/resourceGroups/([^/]+)/providers/[^/]+/[^/]+/([^/]+)(?:/projects/([^/]+))?$"
	$match = [regex]::Match($Id, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
	if ($match.Success) {
		$projectName = $match.Groups[4].Value
		if (-not $projectName) {
			$projectName = $match.Groups[3].Value
		}
		return @{
			SubscriptionId = $match.Groups[1].Value
			ResourceGroup  = $match.Groups[2].Value
			ResourceName   = $match.Groups[3].Value
			ProjectName    = $projectName
		}
	}
	return $null
}

$target = Parse-ResourceId $resourceId
if (-not $target) {
	Write-Host "${Red}ERROR: Could not parse resource ID:${Reset}"
	Write-Host "   $resourceId"
	Write-Host ""
	Write-Host "${Yellow}Expected format:${Reset}"
	Write-Host "   /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{name}/projects/{project}"
	exit 1
}

Write-Host "${Green}OK: Parsed resource ID:${Reset}"
Write-Host "   Subscription : ${Cyan}$($target.SubscriptionId)${Reset}"
Write-Host "   Resource Group: ${Cyan}$($target.ResourceGroup)${Reset}"
Write-Host "   Resource Name : ${Cyan}$($target.ResourceName)${Reset}"
Write-Host "   Project Name  : ${Cyan}$($target.ProjectName)${Reset}"

if (-not $endpoint) {
	$endpoint = "https://$($target.ResourceName).services.ai.azure.com/api/projects/$($target.ProjectName)"
	Write-Host "   Endpoint      : ${Cyan}${endpoint}${Reset}  (derived)"
} else {
	Write-Host "   Endpoint      : ${Cyan}${endpoint}${Reset}  (user-provided)"
}

$source = $null
if ($sourceResourceId) {
	$source = Parse-ResourceId $sourceResourceId
	if (-not $source) {
		Write-Host "${Red}ERROR: Could not parse --source-resource-id${Reset}"
		exit 1
	}
	if (-not $sourceEndpoint) {
		$sourceEndpoint = "https://$($source.ResourceName).services.ai.azure.com/api/projects/$($source.ProjectName)"
	}
	Write-Host ""
	Write-Host "${Green}OK: Source (cross-project):${Reset}"
	Write-Host "   Resource : ${Cyan}$($source.ResourceName)${Reset}"
	Write-Host "   Project  : ${Cyan}$($source.ProjectName)${Reset}"
	Write-Host "   Endpoint : ${Cyan}${sourceEndpoint}${Reset}"
}

Write-Host ""
try {
	$null = Get-Command az -ErrorAction Stop
	Write-Host "${Green}OK: Azure CLI found${Reset}"
} catch {
	Write-Host "${Red}ERROR: Azure CLI (az) not found on PATH.${Reset}"
	exit 1
}

$pythonCmd = $null
foreach ($cmd in @("python", "python3")) {
	try {
		$null = Get-Command $cmd -ErrorAction Stop
		$pythonCmd = $cmd
		break
	} catch { }
}
if (-not $pythonCmd) {
	Write-Host "${Red}ERROR: Python not found. Install Python 3.10+ or use the Docker scripts instead.${Reset}"
	exit 1
}
Write-Host "${Green}OK: Python found ($pythonCmd)${Reset}"

Write-Host ""
Write-Host "${Blue}Checking Python dependencies...${Reset}"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$requirementsPath = Join-Path $scriptDir "requirements.txt"
$depCheckScript = @'
import importlib.util
import sys

mods = ["requests", "azure.identity", "azure.ai.projects", "pandas", "urllib3"]
missing = [module for module in mods if importlib.util.find_spec(module) is None]
print("|".join(missing))
sys.exit(0 if not missing else 1)
'@
$depCheck = & $pythonCmd -c $depCheckScript 2>$null
if ($LASTEXITCODE -ne 0) {
	Write-Host "${Yellow}WARN: Missing required Python packages. Installing from requirements.txt...${Reset}"
	& $pythonCmd -m pip install -r $requirementsPath
	if ($LASTEXITCODE -ne 0) {
		Write-Host "${Red}ERROR: Failed to install Python dependencies.${Reset}"
		exit 1
	}
	Write-Host "${Green}OK: Python dependencies installed${Reset}"
} else {
	Write-Host "${Green}OK: Python dependencies already installed${Reset}"
}

Write-Host ""
$needLogin = $true
try {
	$acct = az account show 2>$null | ConvertFrom-Json
	if ($acct.id) {
		Write-Host "${Green}OK: Already logged in as: $($acct.user.name)${Reset}"
		$needLogin = $false
	}
} catch { }

if ($needLogin) {
	Write-Host "${Yellow}Not logged in. Starting device-code login...${Reset}"
	az login --use-device-code
	if ($LASTEXITCODE -ne 0) {
		Write-Host "${Red}ERROR: Azure login failed.${Reset}"
		exit 1
	}
	Write-Host "${Green}OK: Login successful${Reset}"
}

Write-Host ""
Write-Host "${Blue}Setting subscription and discovering tenant...${Reset}"
az account set --subscription $target.SubscriptionId 2>$null
if ($LASTEXITCODE -ne 0) {
	Write-Host "${Red}ERROR: Could not switch to subscription: $($target.SubscriptionId)${Reset}"
	exit 1
}

$tenantId = (az account show --query tenantId -o tsv).Trim()
if (-not $tenantId -or $tenantId.Length -lt 10) {
	Write-Host "${Red}ERROR: Could not discover tenant ID for subscription $($target.SubscriptionId)${Reset}"
	exit 1
}
Write-Host "${Green}OK: Tenant: ${Cyan}${tenantId}${Reset}"

$sourceTenant = $tenantId
if ($source -and $source.SubscriptionId -ne $target.SubscriptionId) {
	$sourceTenant = (az account show --subscription $source.SubscriptionId --query tenantId -o tsv 2>$null).Trim()
	if (-not $sourceTenant) {
		Write-Host "${Yellow}WARN: Could not discover source tenant. Assuming same tenant.${Reset}"
		$sourceTenant = $tenantId
	} else {
		Write-Host "${Green}OK: Source tenant: ${Cyan}${sourceTenant}${Reset}"
	}
}

Write-Host ""
Write-Host "${Blue}Acquiring access tokens...${Reset}"
$targetToken = (az account get-access-token --scope "https://ai.azure.com/.default" --tenant $tenantId --query accessToken -o tsv).Trim()
if (-not $targetToken -or $targetToken.Length -lt 50) {
	Write-Host "${Red}ERROR: Failed to acquire target Azure AI token.${Reset}"
	exit 1
}
$sourceToken = if ($sourceTenant -eq $tenantId) { $targetToken } else { (az account get-access-token --scope "https://ai.azure.com/.default" --tenant $sourceTenant --query accessToken -o tsv).Trim() }
if (-not $sourceToken -or $sourceToken.Length -lt 50) {
	Write-Host "${Red}ERROR: Failed to acquire source Azure AI token.${Reset}"
	exit 1
}
$sourceCompatToken = (az account get-access-token --scope "https://cognitiveservices.azure.com/.default" --tenant $sourceTenant --query accessToken -o tsv).Trim()
if (-not $sourceCompatToken -or $sourceCompatToken.Length -lt 50) {
	Write-Host "${Yellow}WARN: Failed to acquire legacy OpenAI-compatible token. Legacy assistants endpoint may fail.${Reset}"
}

$env:PRODUCTION_TOKEN = $targetToken
$env:AZ_TOKEN = $sourceToken
$env:AZ_TOKEN_SCOPE = "https://ai.azure.com/.default"
if ($sourceCompatToken -and $sourceCompatToken.Length -gt 50) {
	$env:OPENAI_COMPAT_TOKEN = $sourceCompatToken
	$env:OPENAI_COMPAT_TOKEN_SCOPE = "https://cognitiveservices.azure.com/.default"
}

Push-Location $scriptDir
$migrationArgs = @()
if ($source) {
	$migrationArgs += "--project-endpoint", $sourceEndpoint
	if ($sourceTenant -ne $tenantId) {
		$migrationArgs += "--source-tenant", $sourceTenant
	}
} else {
	$migrationArgs += "--project-endpoint", $endpoint
}
$migrationArgs += "--production-resource", $target.ResourceName
$migrationArgs += "--production-subscription", $target.SubscriptionId
$migrationArgs += "--production-tenant", $tenantId
$migrationArgs += "--production-endpoint", $endpoint
$migrationArgs += $passthrough

Write-Host ""
Write-Host "${Blue}Starting migration...${Reset}"
Write-Host "==============================================================="
Write-Host "${Cyan}Command:${Reset}"
Write-Host "  $pythonCmd v1_to_v2_migration.py $($migrationArgs -join ' ')"
Write-Host ""

& $pythonCmd v1_to_v2_migration.py @migrationArgs

$exitCode = $LASTEXITCODE
Pop-Location
if ($exitCode -eq 0) {
	Write-Host ""
	if ($listMode) {
		Write-Host "${Green}${Bold}OK: Inventory listing completed successfully!${Reset}"
	} else {
		Write-Host "${Green}${Bold}OK: Migration completed successfully!${Reset}"
	}
} else {
	Write-Host ""
	Write-Host "${Red}ERROR: Migration failed with exit code: $exitCode${Reset}"
}
exit $exitCode
