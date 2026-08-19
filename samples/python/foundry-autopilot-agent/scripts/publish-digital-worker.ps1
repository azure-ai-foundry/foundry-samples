param(
    [Parameter(Mandatory = $true)]
    [string]$BlueprintClientId
)

$ErrorActionPreference = "Stop"

Write-Host "Starting post-provision script..."

Write-Host "Resources were deployed to: location $env:LOCATION subscriptionId $env:SUBSCRIPTION_ID agentName $env:AGENT_NAME"

$AzureAIProjectEndpoint = $env:AZURE_AI_PROJECT_ENDPOINT
$AgentName = $env:AGENT_NAME

$agentPublishUrl = "$($AzureAIProjectEndpoint)/agents/$($AgentName)/microsoft365/publish?api-version=2025-11-15-preview"

# Construct JSON body based on Microsoft365PublishRequest
$body = @{
    agentDisplayName            = $env:AGENT_NAME
    publishAsAutopilot          = $true
    publishScope                = "Tenant"
    appVersion                  = "1.0.0"
    canRespondWithoutMention    = $true
    shortDescription            = "Foundry A365 Agent deployed via Azure Developer CLI"
    fullDescription             = "A Foundry A365 agent example that demonstrates integration with Microsoft 365 and Azure Cognitive Services."
    developerName               = "Azure Developer"
    developerWebsiteUrl         = "https://azure.microsoft.com"
    privacyUrl                  = "https://privacy.microsoft.com"
    termsOfUseUrl               = "https://www.microsoft.com/legal/terms-of-use"
    useAgenticUserTemplate      = $true
    agenticUserTemplate         = @{
            Id                         = "digitalWorkerTemplate"
            File                       = "agenticUserTemplateManifest.json"
            SchemaVersion              = "0.1.0-preview"
            AgentIdentityBlueprintId   = $BlueprintClientId
            CommunicationProtocol      = "activityProtocol"
    }
}

$jsonBody = $body | ConvertTo-Json -Depth 10

$aiAzureToken = az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv --tenant $env:TENANT_ID


Write-Host "Sending Microsoft 365 publish request to $agentPublishUrl..."
Write-Host "JSON Body:"
Write-Host $jsonBody

# Send POST request

try{
    $response = Invoke-RestMethod -Uri $agentPublishUrl `
    -Method Post `
    -Headers @{
        "Content-Type" = "application/json"
        "Accept"       = "application/json"
        "Authorization" = "Bearer $($aiAzureToken)"
    } `
    -Body $jsonBody

    Write-Host ""
    Write-Host "Response:"
    $response | ConvertTo-Json -Depth 5 | Write-Host
}
catch {
        $err = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($err.error.code -eq "UserError" -and
        $err.error.message -like "*version already exists*") {

        Write-Host "A digital worker is already published with this version. Ignoring."
    }
    else {
        throw
    }
}

Write-Host ""
Write-Host "Publish digital worker script finished."
