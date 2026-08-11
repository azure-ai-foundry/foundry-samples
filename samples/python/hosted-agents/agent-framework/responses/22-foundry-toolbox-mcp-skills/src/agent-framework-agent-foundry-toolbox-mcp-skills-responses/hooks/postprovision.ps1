#!/usr/bin/env pwsh
# azd postprovision hook (Windows / pwsh).
#
# Runs automatically after `azd provision`. It creates the two bundled Agent
# Skills in the Foundry project and then creates the toolbox that serves them,
# storing TOOLBOX_ENDPOINT so the agent can reach it — collapsing the manual
# "Building the toolbox from zero" steps into a single `azd provision`.
#
# The bundled toolbox.yaml references the skills by name, so the skills must
# exist before `azd ai toolbox create` runs.

$ErrorActionPreference = "Stop"

# PowerShell does not stop on a non-zero exit code from a native command (like
# azd), so check $LASTEXITCODE after each native call and fail loudly.
function Invoke-Checked {
    param([scriptblock] $Script, [string] $What)
    & $Script
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)." }
}

# Run from the project directory (the parent of hooks/) so toolbox.yaml and
# skills/ resolve no matter where azd invokes the hook from.
Set-Location (Split-Path -Parent $PSScriptRoot)

Write-Host "Provisioning the bundled skills..."
# Create each skills/<name>/SKILL.md as a Foundry skill. 'create' fails if the
# skill already exists (e.g. a repeat azd provision), so fall back to 'update',
# which adds a new default version and keeps history.
foreach ($skillDir in Get-ChildItem -Path skills -Directory) {
    $name = $skillDir.Name
    $file = Join-Path $skillDir.FullName "SKILL.md"
    Write-Host "  Ensuring skill '$name' from $file..."
    azd ai skill create $name --file $file --no-prompt
    if ($LASTEXITCODE -ne 0) {
        Invoke-Checked { azd ai skill update $name --file $file --no-prompt } "skill update '$name'"
    }
}

Write-Host "Creating the skills toolbox..."
# Toolbox versions are immutable and 'create' has no upsert flag, so skip it if
# the toolbox already exists. 'toolbox show' exits 0 when it exists. azd writes
# diagnostics to stderr, which PowerShell turns into a terminating error under
# "Stop", so probe with errors non-terminating and decide on the exit code.
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
azd ai toolbox show maf-skills-toolbox *> $null
$toolboxExists = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $prevEap
if ($toolboxExists) {
    Write-Host "Toolbox maf-skills-toolbox already exists; skipping create."
}
else {
    Invoke-Checked { azd ai toolbox create maf-skills-toolbox --from-file ./toolbox.yaml --no-prompt } "toolbox create"
}

# The toolbox's unversioned MCP alias is deterministic from the project endpoint
# and always resolves to the default version.
$proj = (azd env get-value FOUNDRY_PROJECT_ENDPOINT).TrimEnd('/')
$toolbox = "$proj/toolboxes/maf-skills-toolbox/mcp?api-version=v1"
Invoke-Checked { azd env set TOOLBOX_ENDPOINT $toolbox } "env set TOOLBOX_ENDPOINT"

Write-Host "Done. TOOLBOX_ENDPOINT = $toolbox"
