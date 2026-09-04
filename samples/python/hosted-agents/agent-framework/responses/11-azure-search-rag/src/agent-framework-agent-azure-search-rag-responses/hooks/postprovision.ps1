#!/usr/bin/env pwsh
# azd postprovision hook (Windows / pwsh).
#
# Creates and seeds the sample index after the search service exists.

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param([scriptblock] $Script, [string] $What)
    & $Script
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)." }
}

# Run beside provision_index.py regardless of azd's working directory.
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not $env:AZURE_SEARCH_ENDPOINT) {
    throw "AZURE_SEARCH_ENDPOINT is not set. Provide an existing Azure AI Search service endpoint."
}

if (-not $env:AZURE_SEARCH_INDEX_NAME) {
    $env:AZURE_SEARCH_INDEX_NAME = "contoso-outdoors"
    Invoke-Checked {
        azd env set AZURE_SEARCH_INDEX_NAME $env:AZURE_SEARCH_INDEX_NAME
    } "env set AZURE_SEARCH_INDEX_NAME"
}

Write-Host "Provisioning the Azure AI Search index '$($env:AZURE_SEARCH_INDEX_NAME)'..."
Invoke-Checked {
    python -m pip install -q azure-search-documents azure-identity python-dotenv
} "pip install"
Invoke-Checked { python provision_index.py } "provision_index.py"
