# Deploy Samples

All the samples in the repo can be deployed to Azure, requiring 3 resources: 

- An Entra ID application
- The application deployed exposing an HTTPS URL, this can be any compute service such as AppServices, ContainerApps or ContainerInstances. This resource needs to be configured with the ApplicationId, TenantId and a secret (these samples use SingleTenant and ClientSecrets)
- The Bot resource configured with the URL obtained in the previous step

>Note: These steps are consolidated in the script `deploy-bot-app.ps1`, see usage instructions at the end of this doc.

## 1. Create the App Registration in EntraID

Each bot has an identity provided by EntraId, it can be configured as SingleTenant, MultiTenant or UserManagedIdentity, and requires a secret to produce access tokens

### Create manually from the Azure portal

Navigate to http://entra.microsoft.com and create a new AppRegistration, along with a secret that you will need to 

### Create from Azure CLI

```ps
$appId = az ad app create --display-name $botName --sign-in-audience "AzureADMyOrg" --query appId | ConvertFrom-Json
```

## 2. Deploy the dotnet code as a container to ContainerApps

.NET8 includes support to package the application as a docker image by using:

```
dotnet publish /t:PublishContainer
```

The image needs to be published to a container registry, such as Azure Container Registry. To publish the image you need to tag it with the ACR instance name, authenticate to ACR and then push it.

### Push the image with Visual Studio

Visual Studio 2020 includes support to push the image from the IDE, right click in the project and select Publish -> To Azure -> To Azure Container Registry

### Push the image from the CLI

To push the image from the CLI, assuming your ACR is named "my-bot-images.azurecr.io" and your image is called `echobot` with tag `latest`:

```bash
docker tag echobot:latest my-bot-images.azurecr.io/echobot:latest
az acr login -n my-bot-images
docker push my-bot-images.azurecr.io/echobot:latest
```

### Deploy to Azure Container Apps

You can create a new ContainerApp environment to host your app. The app needs to be configured with the AppId and Secret obtained previously, you can follow the instructions from Azure Portal, or use the next AZ CLI command

```ps
$secretJson = az ad app credential reset --id $appId | ConvertFrom-Json
az containerapp up `
    --resource-group $resourceGroup `
    --name $containerName `
    --image $botImage `
    --environment bot-apps-$resourceGroup `
    --ingress external `
    --env-vars `
        Connections__BotServiceConnection__Settings__TenantId=$($secretJson.tenant) `
        Connections__BotServiceConnection__Settings__ClientId=$($secretJson.appId) `
        Connections__BotServiceConnection__Settings__AuthorityEndpoint="https://login.microsoftonline.com/$($secretJson.tenant)" `
        Connections__BotServiceConnection__Settings__ClientSecret=$($secretJson.password) `
        Logging__LogLevel__Microsoft_AspNetCore="Information"
```

Once the app is created, you should get the URL to access the app.

## 3. Create the Bot service instance

The last step is to create the Azure Bot service instance, and provide the messaging endpoint, ususally it will be the URL obtained in the previous step appending `/api/messages`

You can obtain the URL, and automate the bot instance creation with the following CLI command:

```
$fqdn = az containerapp show --resource-g  $resourceGroup  --name $containerName --query properties.configuration.ingress.fqdn -o tsv
$endpoint = "https://$fqdn/api/messages"

$botJson = az bot create `
    --app-type SingleTenant `
    --appid $appId `
    --tenant-id $($secretJson.tenant) `
    --name $botName `
    --resource-group $resourceGroup `
    --endpoint $endpoint

$teamsBotJson = az bot msteams create -n $botName -g $resourceGroup
```

# Usage instructions for `deploy-bot-app.ps1`

This script requires four variables: 

- `resourceGroup`. The Azure resource group to provision all the resources. eg: `my-bot-resources`
- `acrName`. The Azure Container Registry instance name (it needs to be provisioned in advance). eg: `my-bot-images.azurecr.io`
- `acrImage`. The container image including the tag. eg: `echobot:latest`
- `botName`. The name of the bot instance (the name of the container app will be based on this). eg: `my-echobot`

These variables can be set as Environment Variables, when not found the script will ask for user input.

## Usage

```ps
deploy-bot-app.ps1
```