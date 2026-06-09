# Build Docker image using Azure Container Registry (ACR) Build
# This script uses ACR Tasks to build the image in the cloud instead of locally

Set-Location "$($PSScriptRoot)/../src/workstream_manager_agent"

Remove-Item "./publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "./.vs" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "./bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "./obj" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish "./WorkstreamManagerAgent.csproj" -c Release -o "./publish"

$authorityEndpoint = "https://login.microsoftonline.com/$($env:TENANT_ID)"
$azureOpenAIEndpoint = "https://$($env:ACCOUNT_NAME).openai.azure.com/"

$modelDeploymentName = $env:MODEL_DEPLOYMENT_NAME
if ([string]::IsNullOrWhiteSpace($modelDeploymentName)) {
    $appSettingsPath = Join-Path $PSScriptRoot "../src/workstream_manager_agent/appsettings.json"
    if (Test-Path $appSettingsPath) {
        $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        $modelDeploymentName = $appSettings.ModelDeployment
    }
}
if ([string]::IsNullOrWhiteSpace($modelDeploymentName)) {
    $modelDeploymentName = "gpt-5-chat"
}


$acrLoginServer = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT

# split the login server to get the registry name
$registryName = $acrLoginServer.Split(".")[0]

$imageName = "workstream-manager-agent1:latest"

Write-Host "Building image using ACR Build in registry: $registryName"

# Build image using ACR Build (builds in the cloud)
az acr build `
    --registry $registryName `
    --subscription $env:SUBSCRIPTION_ID `
    --image $imageName `
    --file "./foundry-infra/Dockerfile" `
    --build-arg BLUEPRINT_CLIENT_ID=$env:AGENT_IDENTITY_BLUEPRINT_ID `
    --build-arg AUTHORITY_ENDPOINT=$authorityEndpoint `
    --build-arg AZURE_OPENAI_ENDPOINT=$azureOpenAIEndpoint `
    --build-arg MODEL_DEPLOYMENT=$modelDeploymentName `
    --build-arg PROJECT_DEFAULT_INSTANCE_CLIENT_ID=$env:PROJECT_DEFAULT_INSTANCE_CLIENT_ID `
    --build-arg WORK_ITEMS_TABLE_SERVICE_URI=$env:WORK_ITEMS_TABLE_SERVICE_URI `
    --build-arg WORK_ITEMS_TABLE_NAME=$env:WORK_ITEMS_TABLE_NAME `
    .

if ($LASTEXITCODE -ne 0) {
    throw "ACR build failed with exit code $LASTEXITCODE"
}

Write-Host "Image built and pushed successfully: $acrLoginServer/$imageName"

Remove-Item "./publish" -Recurse -Force -ErrorAction SilentlyContinue

