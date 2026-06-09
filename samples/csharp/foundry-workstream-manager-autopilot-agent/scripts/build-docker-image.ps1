Set-Location  "$($PSScriptRoot)/../src/workstream_manager_agent"

Remove-Item "./publish" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish "./WorkstreamManagerAgent.csproj" -c Release -o "./publish"


$authorityEndpoint = "https://login.microsoftonline.com/$($env:TENANT_ID)"
$azureOpenAIEndpoint = "https://$($env:ACCOUNT_NAME).openai.azure.com/"
    
$projectClientId = az ad sp show --id $env:PROJECT_PRINCIPAL_ID --query appId -o tsv

# if the projectClientId is null or empty, throw an error
if ([string]::IsNullOrEmpty($projectClientId)) {
    throw "Failed to get project client ID for principal ID $($env:PROJECT_PRINCIPAL_ID)"
}

docker build -t workstream-manager-agent:a365preview001 `
    --build-arg BLUEPRINT_CLIENT_ID=$env:AGENT_IDENTITY_BLUEPRINT_ID `
    --build-arg AUTHORITY_ENDPOINT=$authorityEndpoint `
    --build-arg FEDERATED_CLIENT_ID=$projectClientId `
    --build-arg AZURE_OPENAI_ENDPOINT=$azureOpenAIEndpoint `
    --build-arg MODEL_DEPLOYMENT='gpt-4o' `
    --build-arg PROJECT_DEFAULT_INSTANCE_CLIENT_ID=$env:PROJECT_DEFAULT_INSTANCE_CLIENT_ID `
    --build-arg WORK_ITEMS_TABLE_SERVICE_URI=$env:WORK_ITEMS_TABLE_SERVICE_URI `
    --build-arg WORK_ITEMS_TABLE_NAME=$env:WORK_ITEMS_TABLE_NAME `
    -f "./foundry-infra/Dockerfile" .

$acrLoginServer = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT

# split the login server to get the registry name
$registryName = $acrLoginServer.Split(".")[0]

docker tag workstream-manager-agent:a365preview001 $acrLoginServer/workstream-manager-agent:a365preview001

az acr login --name $registryName

docker push $acrLoginServer/workstream-manager-agent:a365preview001

Remove-Item "./publish" -Recurse -Force -ErrorAction SilentlyContinue

