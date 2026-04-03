#!/usr/bin/env pwsh
# ─────────────────────────────────────────────────────────────────────
# migrate-docker.ps1 — Simplified v1→v2 migration via Docker
# ─────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"

$Green  = "`e[32m"
$Blue   = "`e[34m"
$Yellow = "`e[33m"
$Red    = "`e[31m"
$Cyan   = "`e[36m"
$Bold   = "`e[1m"
$Reset  = "`e[0m"

function Get-AzCliCommand {
	$azCommand = Get-Command az -ErrorAction SilentlyContinue
	if ($azCommand) { return $azCommand.Source }
	foreach ($path in @("C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd", "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd")) {
		if (Test-Path $path) { return $path }
	}
	return $null
}

function Ensure-AzCli {
	$azPath = Get-AzCliCommand
	if ($azPath) { return $azPath }
	Write-Host "${Red}❌ Azure CLI is required on the host for sign-in.${Reset}"
	return $null
}

$resourceId       = $null
$endpoint         = $null
$sourceResourceId = $null
$sourceEndpoint   = $null
$passthrough      = @()
$listMode         = $false

for ($i = 0; $i -lt $args.Count; $i++) {
	switch ($args[$i]) {
		"--resource-id"        { $resourceId       = $args[++$i] }
		"--endpoint"           { $endpoint         = $args[++$i] }
		"--source-resource-id" { $sourceResourceId = $args[++$i] }
		"--source-endpoint"    { $sourceEndpoint   = $args[++$i] }
		"--list"               { $listMode = $true; $passthrough += $args[$i] }
		default                { $passthrough += $args[$i] }
	}
}

Write-Host ""
Write-Host "${Blue}${Bold}===============================================================${Reset}"
Write-Host "${Blue}${Bold}  v1 to v2 Agent Migration (Docker + simplified)${Reset}"
Write-Host "${Blue}${Bold}===============================================================${Reset}"
Write-Host ""

$azCli = Ensure-AzCli
if (-not $azCli) { exit 1 }

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

if (-not $resourceId) {
	Write-Host "${Red}ERROR: Missing required ${Bold}--resource-id${Reset}"
	exit 1
}

$target = Parse-ResourceId $resourceId
if (-not $target) {
	Write-Host "${Red}ERROR: Could not parse resource ID.${Reset}"
	exit 1
}

if (-not $endpoint) {
	$endpoint = "https://$($target.ResourceName).services.ai.azure.com/api/projects/$($target.ProjectName)"
}

$source = $null
$sourceEP = $null
if ($sourceResourceId) {
	$source = Parse-ResourceId $sourceResourceId
	if (-not $source) {
		Write-Host "${Red}ERROR: Could not parse --source-resource-id${Reset}"
		exit 1
	}
	if (-not $sourceEndpoint) {
		$sourceEP = "https://$($source.ResourceName).services.ai.azure.com/api/projects/$($source.ProjectName)"
	} else {
		$sourceEP = $sourceEndpoint
	}
}

try {
	docker info 2>$null | Out-Null
} catch {
	Write-Host "${Red}ERROR: Docker is not running. Please start Docker Desktop.${Reset}"
	exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir
docker build -t v1-to-v2-migration .
if ($LASTEXITCODE -ne 0) {
	Write-Host "${Red}ERROR: Docker build failed.${Reset}"
	Pop-Location
	exit $LASTEXITCODE
}

& $azCli account set --subscription $target.SubscriptionId --output none 2>$null
$tenantId = (& $azCli account show --query tenantId -o tsv).Trim()
if (-not $tenantId) {
	Write-Host "${Red}ERROR: Could not discover tenant ID.${Reset}"
	Pop-Location
	exit 1
}

$aiToken = (& $azCli account get-access-token --scope "https://ai.azure.com/.default" --tenant $tenantId --query accessToken -o tsv).Trim()
$openAiCompatToken = (& $azCli account get-access-token --scope "https://cognitiveservices.azure.com/.default" --tenant $tenantId --query accessToken -o tsv).Trim()
if (-not $aiToken -or $aiToken.Length -lt 50) {
	Write-Host "${Red}ERROR: Failed to acquire Azure AI token on the host.${Reset}"
	Pop-Location
	exit 1
}

$migrationArgs = @()
if ($source) {
	$migrationArgs += "--project-endpoint", $sourceEP
} else {
	$migrationArgs += "--project-endpoint", $endpoint
}
$migrationArgs += "--production-resource", $target.ResourceName
$migrationArgs += "--production-subscription", $target.SubscriptionId
$migrationArgs += "--production-tenant", $tenantId
$migrationArgs += "--production-endpoint", $endpoint
$migrationArgs += $passthrough

$azureConfigDir = "$env:USERPROFILE\.azure"
$dockerEnv = @(
	"--network", "host"
	"-e", "DOCKER_CONTAINER=true"
	"-e", "TARGET_SUBSCRIPTION=$($target.SubscriptionId)"
	"-e", "AZ_TOKEN=$aiToken"
	"-e", "AZ_TOKEN_SCOPE=https://ai.azure.com/.default"
	"-e", "PRODUCTION_TOKEN=$aiToken"
	"-v", "${azureConfigDir}:/home/migration/.azure"
)
if ($openAiCompatToken -and $openAiCompatToken.Length -gt 50) {
	$dockerEnv += "-e", "OPENAI_COMPAT_TOKEN=$openAiCompatToken"
	$dockerEnv += "-e", "OPENAI_COMPAT_TOKEN_SCOPE=https://cognitiveservices.azure.com/.default"
}
if (Test-Path ".env") {
	Get-Content ".env" | ForEach-Object {
		if ($_ -match "^([^#=]+)=(.*)$") {
			$dockerEnv += "-e", "$($matches[1].Trim())=$($matches[2].Trim())"
		}
	}
}

docker run --rm -it `
	@dockerEnv `
	v1-to-v2-migration `
	@migrationArgs

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
