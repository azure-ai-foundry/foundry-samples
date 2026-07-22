Write-Host "========================================"
Write-Host "  Playwright Workspace Connection Setup"
Write-Host "========================================"

# ── Check if already configured ───────────────────────────────────────────────

$connectionConfigured = $null
$existingToolbox = $null
$output = azd env get-value PLAYWRIGHT_CONNECTION_CONFIGURED 2>&1
if ($LASTEXITCODE -eq 0 -and $output -and $output.Trim() -eq "true") {
    $connectionConfigured = $true
}
$toolBoxName = azd env get-value TOOLBOX_NAME 2>&1
if ($LASTEXITCODE -eq 0 -and $toolBoxName) {
    $existingToolbox = $toolBoxName.Trim()
}

if ($connectionConfigured -and $existingToolbox) {
    Write-Host "Playwright connection already configured (toolbox: $existingToolbox)"
    exit 0
}

$scriptDir = Split-Path -Parent $PSCommandPath

# ── Step 1: Setup Playwright connection (skip if already done) ────────────────

if (-not $connectionConfigured) {
    & "$scriptDir\setup-playwright.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "Playwright connection exists. Skipping connection setup..."
}

# ── Step 2: Setup Toolbox ─────────────────────────────────────────────────────

& "$scriptDir\setup-toolbox.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }