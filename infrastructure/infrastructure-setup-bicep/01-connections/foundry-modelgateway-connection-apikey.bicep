/*
Creates a ModelGateway connection on a Foundry project.

What this does:
  Adds a connection (of category 'ModelGateway') to a project on your LOCAL
  Foundry account. The connection points at the OpenAI-compatible endpoint
  of a REMOTE Foundry account, so model calls made against the local project
  are routed to model deployments hosted on the remote account.

  Authentication uses the remote account's API key (looked up at deploy time
  via listKeys() — no key needs to be passed in).

Terminology used below:
  - local  = the account/project where this connection resource is created
             (also the deployment resource group, unless overridden).
  - remote = the account whose model deployments will be called through
             the connection.

Deploy:
  az deployment group create \
    --subscription <subscription-id> \
    --resource-group <rg-of-local-account> \
    --template-file foundry-modelgateway-connection-apikey.bicep \
    --parameters \
      connectionName=<connection-name> \
      localAccountName=<local-account> \
      localProjectName=<local-project> \
      remoteAccountName=<remote-account> \
      remoteAccountResourceGroup=<rg-of-remote-account>   # only needed if the remote account lives in a different RG than the deployment
*/

@description('Name of the new ModelGateway connection to create on the local project. This is the name that will appear in the Foundry portal.')
param connectionName string

@description('Name of the local Foundry (AIServices) account that hosts the project where this connection will be created. Must already exist in the deployment resource group.')
param localAccountName string

@description('Name of the project (under the local account) that this connection will be attached to. Must already exist.')
param localProjectName string

@description('Name of the remote Foundry (AIServices) account whose model deployments will be invoked through this connection. The connection target URL and API key are both derived from this account.')
param remoteAccountName string

@description('Resource group of the remote Foundry account. Defaults to the deployment resource group (i.e. assumes the remote account is in the same RG as the local account). Set this explicitly only if the remote account is in a different RG — otherwise the deployment fails with ResourceNotFound when it tries to listKeys() on the remote account.')
param remoteAccountResourceGroup string = resourceGroup().name

@description('If true, the connection is visible to every project on the local account (and shows up in the Foundry portal\'s "Admin-connected models" picker). If false, only the local project listed above can use it. Default: true.')
param isSharedToAll bool = true

// List of Models: Edit this list of models based on what you need to expose. These models should be available on the remote account
@description('List of model deployments available on the remote account that should be exposed through this connection. Each entry must match a real deployment on the remote account (same name, model name, and version). Embedding the list here lets clients resolve "<connection-name>/<deployment-name>" without an extra service lookup. Override the default to match the deployments on YOUR remote account.')
param staticModels array = [
  { name: 'gpt-4.1-mini'    , properties: { model: { name: 'gpt-4.1-mini'    , version: '2025-04-14', format: 'OpenAI' } } }
  { name: 'gpt-4.1'         , properties: { model: { name: 'gpt-4.1'         , version: '2025-04-14', format: 'OpenAI' } } }
  { name: 'gpt-5'           , properties: { model: { name: 'gpt-5'           , version: '2025-08-07', format: 'OpenAI' } } }
  { name: 'gpt-5.1'         , properties: { model: { name: 'gpt-5.1'         , version: '2025-11-13', format: 'OpenAI' } } }
  { name: 'gpt-5.2'         , properties: { model: { name: 'gpt-5.2'         , version: '2025-12-11', format: 'OpenAI' } } }
  { name: 'gpt-5.2-chat'    , properties: { model: { name: 'gpt-5.2-chat'    , version: '2025-12-11', format: 'OpenAI' } } }
  { name: 'DeepSeek-V3-0324', properties: { model: { name: 'DeepSeek-V3-0324', version: '1'         , format: 'OpenAI' } } }
]

// Target URL: the OpenAI-compatible (/openai/v1) endpoint on the remote
// Foundry account. This is the same URL the Foundry portal uses when it
// creates a ModelGateway connection through its UI wizard.
var targetUrl = 'https://${remoteAccountName}.services.ai.azure.com/openai/v1'

resource localAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: localAccountName
}
resource localProject 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' existing = {
  name: localProjectName
  parent: localAccount
}
resource remoteAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: remoteAccountName
  scope: resourceGroup(remoteAccountResourceGroup)
}

// Connection metadata. These five fields are required by the ModelGateway
// connection contract — the portal wizard sets the exact same values, so do
// NOT omit any of them or substitute your own without a strong reason:
//   - models           : stringified JSON of the deployment list above; lets
//                        the portal render the model picker and lets the
//                        runtime resolve "<connection>/<deployment>" locally.
//   - deploymentInPath : 'false' = deployment name is sent in the request
//                        body (OpenAI-style), not in the URL path.
//   - authHeaderName   : header used to pass the API key on every call.
//   - authHeaderFormat : token format placed in that header. '{api_key}'
//                        means the raw key, with no "Bearer " prefix.
//   - customHeaders    : extra static headers to add per request. Empty by
//                        default; must still be present as '{}' .
var metadata = {
  models: string(staticModels)
  deploymentInPath: 'false'
  authHeaderFormat: '{api_key}'
  authHeaderName: 'x-api-key'
  customHeaders: '{}'
}

resource connection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = {
  name: connectionName
  parent: localProject
  properties: {
    category: 'ModelGateway'
    target: targetUrl
    authType: 'ApiKey'
    isSharedToAll: isSharedToAll
    credentials: {
      key: remoteAccount.listKeys().key1
    }
    metadata: metadata
  }
}

output connectionId string = connection.id
output connectionName string = connection.name
output targetUrl string = targetUrl
output deploymentCount int = length(staticModels)
output isSharedToAll bool = isSharedToAll
