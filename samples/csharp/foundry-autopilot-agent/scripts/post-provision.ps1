#!/usr/bin/env pwsh
Write-Host "Starting post-provision script..."

# AZURE_LOCATION is a default azd environment variable
Write-Host "Resources were deployed to: location $env:AZURE_LOCATION blueprintId $env:AZURE_AGENT_IDENTITY_BLUEPRINT_ID subscriptionId $env:AZURE_SUBSCRIPTION_ID agentName $env:AGENT_NAME"

# Write-Host "===============Building and pushing Docker image==============="
& "$PSScriptRoot/build-docker-image-acr.ps1"

Write-Host "===============Creating Agent Version==============="
$agentInfo = & "$PSScriptRoot/agent-creation-script.ps1"
$agentGuid = $agentInfo.AgentGuid
$blueprintClientId = $agentInfo.BlueprintClientId
Write-Host "Agent GUID: $agentGuid, Blueprint Client Id: $blueprintClientId"

Write-Host "===============Publishing digital worker==============="

& "$PSScriptRoot/publish-digital-worker.ps1" -BlueprintClientId $blueprintClientId

Write-Host ""
Write-Host "Post-provision script finished."
