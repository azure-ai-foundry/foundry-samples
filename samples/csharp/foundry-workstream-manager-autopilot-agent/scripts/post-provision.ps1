#!/usr/bin/env pwsh
Write-Host "Starting post-provision script..."

# AZURE_LOCATION is a default azd environment variable
Write-Host "Resources were deployed to: location $env:AZURE_LOCATION blueprintId $env:AZURE_AGENT_IDENTITY_BLUEPRINT_ID subscriptionId $env:AZURE_SUBSCRIPTION_ID agentName $env:AGENT_NAME"

# Write-Host "===============Building and pushing Docker image==============="
& "$PSScriptRoot/build-docker-image-acr.ps1"

# Write-Host "===============Building and pushing Docker image==============="
# & "$PSScriptRoot/build-docker-image-acr-agenticai-sample.ps1"


Write-Host "===============Creating Agent Version==============="
$agentGuid = & "$PSScriptRoot/agent-creation-script.ps1"

# -----------------------------------------------------------------------------
# One-time digital worker setup: adding the current user as blueprint owner,
# creating the blueprint SP OAuth2 grants (+ inheritable scopes), and publishing
# the digital worker only need to happen on the FIRST successful provision. They
# are idempotent, but they make unnecessary API calls on every code-change
# re-provision, so we persist a marker in the azd environment and skip them once
# completed.
#
# Order matters: become blueprint owner -> declare OAuth2 grants + inheritable
# scopes -> publish. Publishing last ensures the agent's scopes are declared
# before an admin approves the published agent (approval consents the scopes).
#
# A code-only change still rebuilds the image and creates a new agent version
# above (those run every time); the published digital worker references the agent
# GUID, not a specific version, so new versions are served without re-publishing.
#
# To force these steps to run again (e.g. after changing publish metadata or
# blueprint scopes): azd env set DIGITAL_WORKER_SETUP_DONE ""
# -----------------------------------------------------------------------------
$digitalWorkerSetupDone = & azd env get-value DIGITAL_WORKER_SETUP_DONE 2>$null
if ($LASTEXITCODE -ne 0 -or $null -eq $digitalWorkerSetupDone) { $digitalWorkerSetupDone = "" }

if ($digitalWorkerSetupDone.Trim() -eq "true") {
    Write-Host "===============Digital worker one-time setup already completed; skipping publish, OAuth2 grants, and blueprint owner==============="
}
else {
    Write-Host "===============Adding current user as blueprint owner==============="
    try {
        & "$PSScriptRoot/add-current-user-as-blueprint-owner.ps1"
    }
    catch {
        Write-Warning "Failed to add current user as blueprint owner: $($_.Exception.Message)"
        Write-Warning "Continuing without blocking post-provision."
    }

    # oAuth2 grants for blueprint SP (also declares inheritable scopes). Must run
    # before publish so the agent's scopes are declared before an admin approves
    # the published agent (approval is where the declared scopes get consented).
    Write-Host "===============OAuth2 grants for blueprint SP==============="
    & "$PSScriptRoot/create-blueprintsp-oauth2-grants.ps1"

    Write-Host "===============Publishing digital worker==============="
    & "$PSScriptRoot/publish-digital-worker.ps1" -AgentGuid $agentGuid

    # Mark one-time setup complete so subsequent re-provisions skip these steps.
    & azd env set DIGITAL_WORKER_SETUP_DONE true | Out-Null
}


Write-Host ""
Write-Host "Post-provision script finished."
