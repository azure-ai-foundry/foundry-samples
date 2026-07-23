# ── Setup Playwright Workspace Connection ─────────────────────────────────────
# This script sets up the Playwright connection via Bicep deployment.
# In interactive mode it prompts for inputs; in CI / --no-prompt mode it reads
# pre-set azd env vars instead.
#
# Supported env vars (set via `azd env set` for non-interactive use):
#   PLAYWRIGHT_SERVICE_RESOURCE_ID  — ARM resource ID of an existing workspace (omit to create new)
#   PLAYWRIGHT_REGION               — Region for a new workspace (e.g. "eastus"); ignored when using existing
#   PLAYWRIGHT_AUTH_TYPE             — ProjectManagedIdentity (default) or AgenticIdentityToken. ApiKey is interactive-only.

# ── Timeout helper ─────────────────────────────────────────────────────────────
# Prompts with a timeout (default 30s). Returns $Default if no input received.
$PROMPT_TIMEOUT_SECONDS = 60

function Read-HostWithTimeout {
    param([string]$Prompt, [string]$Default = "")
    Write-Host $Prompt -NoNewline
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $input_buf = ""
    while ($sw.Elapsed.TotalSeconds -lt $script:PROMPT_TIMEOUT_SECONDS) {
        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true)
            if ($key.Key -eq 'Enter') { break }
            $input_buf += $key.KeyChar
            Write-Host $key.KeyChar -NoNewline
        }
        Start-Sleep -Milliseconds 100
    }
    Write-Host ""
    if ($sw.Elapsed.TotalSeconds -ge $script:PROMPT_TIMEOUT_SECONDS -and [string]::IsNullOrWhiteSpace($input_buf)) {
        Write-Host "  (no input received within ${script:PROMPT_TIMEOUT_SECONDS}s — using default: $Default)"
        return $Default
    }
    if ([string]::IsNullOrWhiteSpace($input_buf)) { return $Default }
    return $input_buf
}

Write-Host ""
Write-Host "This agent requires a Playwright Workspace connection."
Write-Host ""

# ── Read pre-set values from azd env ──────────────────────────────────────────

$playwrightResourceId = ""
$playwrightRegion = ""
$authType = ""
$output = azd env get-value PLAYWRIGHT_SERVICE_RESOURCE_ID 2>&1
if ($LASTEXITCODE -eq 0 -and $output) { $playwrightResourceId = $output.Trim() }
$output = azd env get-value PLAYWRIGHT_REGION 2>&1
if ($LASTEXITCODE -eq 0 -and $output) { $playwrightRegion = $output.Trim() }
$output = azd env get-value PLAYWRIGHT_AUTH_TYPE 2>&1
if ($LASTEXITCODE -eq 0 -and $output) { $authType = $output.Trim() }

$subscriptionId = (azd env get-value AZURE_SUBSCRIPTION_ID 2>$null)

# ── Determine if we need to prompt ────────────────────────────────────────────

# ── Determine if we can prompt ─────────────────────────────────────────────────
# azd hooks with `interactive: true` always connect stdin, even with --no-prompt.
# For non-interactive use, pre-set the env vars via `azd env set` before running.

$isInteractive = -not [Console]::IsInputRedirected

$needsPrompt = [string]::IsNullOrWhiteSpace($playwrightResourceId) -and [string]::IsNullOrWhiteSpace($playwrightRegion)

if ($needsPrompt) {
    if ($isInteractive) {
        Write-Host "Enter an existing Playwright workspace ARM resource ID"
        Write-Host "  (leave empty to create a new one)"
        Write-Host "  (e.g., /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.LoadTestService/playwrightWorkspaces/{name})"
        $playwrightResourceId = Read-HostWithTimeout -Prompt "> " -Default ""
    } else {
        Write-Host "Non-interactive mode: no PLAYWRIGHT_SERVICE_RESOURCE_ID or PLAYWRIGHT_REGION set."
        Write-Host "  Creating a new workspace in the default region (eastus)."
        $playwrightRegion = "eastus"
    }
}

if ([string]::IsNullOrWhiteSpace($playwrightResourceId)) {
    # Creating a new workspace — need a region
    if ([string]::IsNullOrWhiteSpace($playwrightRegion)) {
        Write-Host ""
        Write-Host "A new Playwright workspace will be created."

        # Region selection — fetch available locations dynamically
        $regionNames = @()
        try {
            $regionNames = az provider show --namespace Microsoft.LoadTestService `
                --subscription $subscriptionId `
                --query "resourceTypes[?resourceType=='playwrightWorkspaces'].locations | [0]" -o json 2>$null | ConvertFrom-Json
        } catch {}

        if (-not $regionNames -or $regionNames.Count -eq 0) {
            $regionNames = @("East US", "East Asia", "West Europe", "West US 3")
        }

        Write-Host "Select region for the new workspace:"
        for ($i = 0; $i -lt $regionNames.Count; $i++) {
            Write-Host "  $($i + 1)) $($regionNames[$i])"
        }
        $regionChoice = Read-HostWithTimeout -Prompt "Select (1-$($regionNames.Count)) [default: 1]: " -Default "1"

        $regionIdx = 0
        if ($regionChoice -match '^\d+$' -and [int]$regionChoice -ge 1 -and [int]$regionChoice -le $regionNames.Count) {
            $regionIdx = [int]$regionChoice - 1
        }
        $playwrightRegion = $regionNames[$regionIdx].ToLower() -replace '\s', ''
        Write-Host "  Using region: $($regionNames[$regionIdx]) ($playwrightRegion)"
    } else {
        Write-Host "Using pre-configured region: $playwrightRegion"
    }
} else {
    Write-Host "Using existing workspace: $playwrightResourceId"
}

# Auth type selection
$apiKey = ""
$isExisting = -not [string]::IsNullOrWhiteSpace($playwrightResourceId)

if ([string]::IsNullOrWhiteSpace($authType)) {
    if ($isInteractive) {
        # Build auth options based on whether resource exists
        $authOptions = @(
            @{ Name = "Project Managed Identity (recommended)"; Value = "ProjectManagedIdentity" }
            @{ Name = "Agent Identity"; Value = "AgenticIdentityToken" }
        )
        if ($isExisting) {
            $authOptions += @{ Name = "API Key"; Value = "ApiKey" }
        }

        Write-Host ""
        Write-Host "Select authentication type:"
        for ($i = 0; $i -lt $authOptions.Count; $i++) {
            Write-Host "  $($i + 1)) $($authOptions[$i].Name)"
        }
        $authChoice = Read-HostWithTimeout -Prompt "Select (1-$($authOptions.Count)) [default: 1]: " -Default "1"

        $authIdx = 0
        if ($authChoice -match '^\d+$' -and [int]$authChoice -ge 1 -and [int]$authChoice -le $authOptions.Count) {
            $authIdx = [int]$authChoice - 1
        }
        $authType = $authOptions[$authIdx].Value
    } else {
        $authType = "ProjectManagedIdentity"
        Write-Host "Non-interactive mode: defaulting to ProjectManagedIdentity auth."
    }
} else {
    Write-Host "Using pre-configured auth type: $authType"
}

# ApiKey auth requires an existing workspace — new workspaces set credentials: null
if ([string]::IsNullOrWhiteSpace($playwrightResourceId) -and $authType -eq "ApiKey") {
    Write-Error "API Key authentication requires an existing Playwright workspace. Set PLAYWRIGHT_SERVICE_RESOURCE_ID or choose a managed identity auth type."
    exit 1
}

if ($authType -eq "ApiKey") {
    $apiKeySecure = Read-Host "Enter the Playwright access token" -AsSecureString
    $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKeySecure))
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Write-Error "Access token is required when using API Key authentication."
        exit 1
    }
}

# ── Deploy Bicep ──────────────────────────────────────────────────────────────

$resourceGroup = (azd env get-value AZURE_RESOURCE_GROUP 2>$null)
$aiAccountName = (azd env get-value AZURE_AI_ACCOUNT_NAME 2>$null)
$aiProjectName = (azd env get-value AZURE_AI_PROJECT_NAME 2>$null)

if ([string]::IsNullOrWhiteSpace($resourceGroup) -or [string]::IsNullOrWhiteSpace($aiAccountName) -or [string]::IsNullOrWhiteSpace($aiProjectName)) {
    Write-Error "AZURE_RESOURCE_GROUP, AZURE_AI_ACCOUNT_NAME, and AZURE_AI_PROJECT_NAME must be set. Provisioning may have failed."
    exit 1
}

Write-Host ""
Write-Host "Deploying Playwright connection..."

$scriptDir = Split-Path -Parent $PSCommandPath
$bicepFile = Join-Path $scriptDir "..\infra-modules\playwright-connection.bicep"

# Build parameters as a JSON file to avoid exposing secrets in process arguments
$paramsObj = @{
    aiFoundryAccountName = @{ value = $aiAccountName }
    aiFoundryProjectName = @{ value = $aiProjectName }
    authType             = @{ value = $authType }
}

if (-not [string]::IsNullOrWhiteSpace($playwrightResourceId)) {
    $paramsObj["playwrightResourceId"] = @{ value = $playwrightResourceId }
}
if (-not [string]::IsNullOrWhiteSpace($playwrightRegion)) {
    $paramsObj["playwrightRegion"] = @{ value = $playwrightRegion }
}
if ($authType -eq "ApiKey") {
    $paramsObj["apiKey"] = @{ value = $apiKey }
}

$paramsFile = [System.IO.Path]::GetTempFileName()
try {
    $paramsObj | ConvertTo-Json -Depth 3 | Set-Content -Path $paramsFile -Encoding UTF8

    $result = az deployment group create `
        --name "playwright-connection-$(Get-Date -Format 'yyyyMMddHHmmss')" `
        --resource-group $resourceGroup `
        --subscription $subscriptionId `
        --template-file $bicepFile `
        --parameters "@$paramsFile" `
        --query "properties.outputs" -o json `
        --only-show-errors
} finally {
    Remove-Item -Path $paramsFile -Force -ErrorAction SilentlyContinue
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to deploy Playwright connection."
    exit 1
}

# ── Store outputs in azd env ──────────────────────────────────────────────────

$outputs = $result | ConvertFrom-Json
$resolvedResourceId = $outputs.playwrightResourceId.value

azd env set PLAYWRIGHT_SERVICE_RESOURCE_ID $resolvedResourceId
azd env set PLAYWRIGHT_AUTH_TYPE $authType
azd env set PLAYWRIGHT_CONNECTION_CONFIGURED "true"

Write-Host ""
Write-Host "✅ Playwright connection created successfully."
Write-Host "   Auth: $authType"
if (-not $isExisting) {
    Write-Host "   Resource ID: $resolvedResourceId"
    $portalUrl = "https://portal.azure.com/#@/resource${resolvedResourceId}"
    Write-Host "   Portal Link: " -NoNewline; Write-Host $portalUrl -ForegroundColor Cyan
}
