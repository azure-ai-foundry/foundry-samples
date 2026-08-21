/*
  ================================================================================
  main.bicep  — AI Gateway happy path (TC01): consume a model via APIM, end to end
  --------------------------------------------------------------------------------
  Self-contained, single-project sample. Deploys EVERYTHING TC01 needs in one
  `az deployment group create`:

    1. A user-assigned managed identity (UAMI) shared by the account and project.
    2. A Microsoft Foundry account (AIServices) + project, both using the UAMI.
    3. A model deployment (default gpt-5.4) on the account.
    4. A public StandardV2 Azure API Management "AI Gateway" with a system-assigned
       MI, RBAC'd "Cognitive Services User" on the account.
    5. The /inference API on APIM with the managed-identity + backend-rewrite policy
       chain, PLUS an LLM token-limit policy (llm-token-limit) on the
       chat-completions operation — this is TC01 "add a policy" (step 4).
    6. A Bring-Your-Own-Model (BYOM) connection on the project that surfaces the
       model as <connectionName>/<modelName> for a prompt agent.

  Why a USER-assigned identity? APIM's validate-azure-ad-token policy must pin the
  project identity's application (client) ID. A system-assigned identity's client
  ID isn't known until after the project exists, which would force a two-step
  deploy. A UAMI's clientId is known at deploy time, so the whole thing deploys in
  one shot. (Established pattern — see templates 17/20/32.)

  This is the self-contained counterpart of ../public-byom-apim, which layers onto
  an EXISTING project + backend account and adds no policy.

  Reference: https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway
  ================================================================================
*/

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Foundry account / project
// ---------------------------------------------------------------------------
@description('Base name for the Foundry account. A short unique suffix is appended.')
@maxLength(9)
param aiServicesName string = 'foundry'

@description('Name of the project created under the account.')
param projectName string = 'gateway-project'

@description('Project description.')
param projectDescription string = 'Single-project AI Gateway happy path (TC01).'

@description('Project display name.')
param projectDisplayName string = 'AI Gateway happy path'

@allowed([
  'australiaeast'
  'canadaeast'
  'eastus'
  'eastus2'
  'francecentral'
  'japaneast'
  'koreacentral'
  'norwayeast'
  'polandcentral'
  'southindia'
  'swedencentral'
  'switzerlandnorth'
  'uaenorth'
  'uksouth'
  'westus'
  'westus2'
  'westus3'
  'westeurope'
  'southeastasia'
  'brazilsouth'
  'germanywestcentral'
  'italynorth'
  'southafricanorth'
  'southcentralus'
])
@description('Region for the account, project, model, and APIM.')
param location string = 'eastus'

// ---------------------------------------------------------------------------
// Model deployment
// ---------------------------------------------------------------------------
@description('Model to deploy and surface through the gateway.')
param modelName string = 'gpt-5.4'

@description('Model format. Example: OpenAI.')
param modelFormat string = 'OpenAI'

@description('Model version. Ensure this version is available in your region: az cognitiveservices account list-models.')
param modelVersion string = '2026-03-05'

@description('Model deployment SKU name. Example: GlobalStandard.')
param modelSkuName string = 'GlobalStandard'

@description('Model deployment capacity in TPM.')
param modelCapacity int = 40

// ---------------------------------------------------------------------------
// APIM AI Gateway
// ---------------------------------------------------------------------------
@description('Globally unique APIM service name. Resolves to <name>.azure-api.net. Leave empty to auto-generate.')
param apimName string = ''

@description('Publisher email required by APIM at create time.')
param publisherEmail string

@description('Publisher organization name required by APIM at create time.')
param publisherName string

// ---------------------------------------------------------------------------
// BYOM connection
// ---------------------------------------------------------------------------
@description('Foundry connection name. Surfaces as <connectionName>/<modelName> in agent code.')
param connectionName string = 'ai-gateway'

@description('Inference API version sent to the backend by Foundry SDK calls.')
param inferenceApiVersion string = '2024-10-21'

// ---------------------------------------------------------------------------
// Token-limit policy (TC01 step 4)
// ---------------------------------------------------------------------------
@description('Tokens-per-minute budget enforced by the gateway. Low by default so throttling is easy to demonstrate.')
param tokensPerMinute int = 1000

@description('Estimate prompt tokens before calling the backend (true) or meter only actual usage returned by the model (false).')
param estimatePromptTokens bool = true

// ---------------------------------------------------------------------------
// Naming
// ---------------------------------------------------------------------------
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)
var accountName = toLower('${aiServicesName}${uniqueSuffix}')
var effectiveApimName = empty(apimName) ? 'apim-${uniqueSuffix}-aigw' : apimName
var uamiName = '${accountName}-uami'

// ===========================================================================
// User-assigned managed identity — shared by account + project so its clientId
// (used to pin APIM's validate-azure-ad-token policy) is known at deploy time.
// ===========================================================================
resource uami 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' = {
  name: uamiName
  location: location
}

// ===========================================================================
// Foundry account (AIServices)
// ===========================================================================
resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: accountName
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uami.id}': {}
    }
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
  }
}

// ===========================================================================
// Model deployment
// ===========================================================================
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: account
  name: modelName
  sku: {
    capacity: modelCapacity
    name: modelSkuName
  }
  properties: {
    model: {
      name: modelName
      format: modelFormat
      version: modelVersion
    }
  }
}

// ===========================================================================
// Project
// ===========================================================================
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: account
  name: projectName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uami.id}': {}
    }
  }
  properties: {
    description: projectDescription
    displayName: projectDisplayName
  }
  // Deploy the model before the project to avoid concurrent account child writes.
  dependsOn: [
    modelDeployment
  ]
}

// ===========================================================================
// Public StandardV2 APIM AI Gateway (reused module)
// ===========================================================================
module apimService '../public-byom-apim/modules/apim-service-public.bicep' = {
  name: 'apim-${uniqueSuffix}-deployment'
  params: {
    location: location
    apimName: effectiveApimName
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

// ===========================================================================
// Grant APIM's MI "Cognitive Services User" on the account (reused module).
// Backend == the same account we just created (single-project topology).
// ===========================================================================
module apimBackendRole '../public-byom-apim/modules/apim-backend-role-assignment.bicep' = {
  name: 'apim-role-${uniqueSuffix}-deployment'
  params: {
    apimPrincipalId: apimService.outputs.apimPrincipalId
    backendAccountName: account.name
  }
}

// ===========================================================================
// /inference API + managed-identity policy chain (reused module).
// projectMiClientId = the UAMI clientId, known at deploy time.
// ===========================================================================
module inferenceApi '../public-byom-apim/modules/apim-inference-api.bicep' = {
  name: 'inference-api-${uniqueSuffix}-deployment'
  params: {
    apimName: apimService.outputs.apimName
    projectMiClientId: uami.properties.clientId
    backendAccountName: account.name
    backendRegion: location
    projectRegion: location
  }
  dependsOn: [
    apimBackendRole
  ]
}

// ===========================================================================
// TC01 step 4 — add a policy: LLM token-limit on the chat-completions
// operation (new module; leaves the shared API-level MI policy untouched).
// ===========================================================================
module tokenLimit 'modules/apim-token-limit-policy.bicep' = {
  name: 'token-limit-${uniqueSuffix}-deployment'
  params: {
    apimName: apimService.outputs.apimName
    apiName: inferenceApi.outputs.apiName
    tokensPerMinute: tokensPerMinute
    estimatePromptTokens: estimatePromptTokens
  }
}

// ===========================================================================
// BYOM model connection on the project, pointing at APIM (reused canonical module)
// ===========================================================================
module byomConnection '../apim/connection-apim.bicep' = {
  name: 'byom-connection-${uniqueSuffix}-deployment'
  params: {
    projectResourceId: project.id
    apimResourceId: apimService.outputs.apimResourceId
    apiName: inferenceApi.outputs.apiName
    connectionName: connectionName
    authType: 'ProjectManagedIdentity'
    isSharedToAll: true
    deploymentInPath: 'true'
    inferenceAPIVersion: inferenceApiVersion
    staticModels: [
      {
        name: modelName
        properties: {
          model: {
            name: modelName
            version: modelVersion
            format: modelFormat
          }
        }
      }
    ]
  }
  dependsOn: [
    tokenLimit
  ]
}

// ===========================================================================
// Outputs
// ===========================================================================
output accountName string = account.name
output projectName string = project.name
output projectResourceId string = project.id
output projectEndpoint string = 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}'
output apimName string = apimService.outputs.apimName
output apimGatewayUrl string = apimService.outputs.apimGatewayUrl
output connectionName string = byomConnection.outputs.connectionName
output modelReference string = '${connectionName}/${modelName}'
output userAssignedIdentityClientId string = uami.properties.clientId
