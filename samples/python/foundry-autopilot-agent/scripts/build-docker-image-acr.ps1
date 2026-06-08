# Build Docker image using Azure Container Registry (ACR) Build
# This script uses ACR Tasks to build the image in the cloud instead of locally

Set-Location "$($PSScriptRoot)/../src/hello_world_a365_agent"

Remove-Item "./__pycache__" -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path . -Filter "__pycache__" -Recurse -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "./.vs" -Recurse -Force -ErrorAction SilentlyContinue

$authorityEndpoint = "https://login.microsoftonline.com/$($env:TENANT_ID)"
$azureOpenAIEndpoint = "https://$($env:ACCOUNT_NAME).openai.azure.com/"


$acrLoginServer = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT

# split the login server to get the registry name
$registryName = $acrLoginServer.Split(".")[0]

$imageName = "hello-world-a365-agent:latest"

Write-Host "Building image using ACR Build in registry: $registryName"

# Build image using ACR Build (builds in the cloud)
az acr build `
    --registry $registryName `
    --image $imageName `
    --file "./foundry-infra/Dockerfile" `
    --build-arg BLUEPRINT_CLIENT_ID=$env:AGENT_IDENTITY_BLUEPRINT_ID `
    --build-arg AUTHORITY_ENDPOINT=$authorityEndpoint `
    --build-arg TENANT_ID=$env:TENANT_ID `
    --build-arg AZURE_OPENAI_ENDPOINT=$azureOpenAIEndpoint `
    --build-arg MODEL_DEPLOYMENT=$env:MODEL_NAME `
    .

if ($LASTEXITCODE -ne 0) {
    throw "ACR build failed with exit code $LASTEXITCODE"
}

Write-Host "Image built and pushed successfully: $acrLoginServer/$imageName"
