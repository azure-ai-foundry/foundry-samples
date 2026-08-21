/*
  ================================================================================
  main.bicep  — TC01 for the AI Gateway tier (preview), steps 1-2
  --------------------------------------------------------------------------------
  Creates the Foundry side of the TC01 happy path so the AI Gateway tier can
  import the model:

    1. A Microsoft Foundry account (AIServices) + project.
    2. A gpt-5.4 model deployment on the account.

  Deliberately simple — NO user-assigned identity, NO APIM service, NO XML
  policies. The AI Gateway tier (https://ai.gateway.azure.com) is a separate,
  portal-provisioned product; you create the gateway and import this model from
  its portal (steps 3-4), then test with samples/test-model-via-gateway.py
  (step 5). See README.md.

  Why local auth stays ENABLED (disableLocalAuth: false):
    The AI Gateway tier "Import from Foundry" wizard uses KEY-BASED backend
    authentication by default — it reads the account's API key at import time and
    sends it in the api-key header to the backend. That requires local (key) auth
    to be enabled on the account. This is what lets you avoid a managed identity.

  Region: the AI Gateway tier is in public preview only in East US 2 and Sweden
  Central, so this template restricts the location to those two regions to keep
  the model co-located with the gateway.

  Verified against:
    https://learn.microsoft.com/azure/api-management/quickstart-ai-gateway-create
    https://learn.microsoft.com/azure/api-management/ai-gateway-setup
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
param projectDescription string = 'AI Gateway tier TC01 happy path.'

@description('Project display name.')
param projectDisplayName string = 'AI Gateway tier TC01'

@allowed([
  'eastus2'
  'swedencentral'
])
@description('Region for the account, project, and model. Restricted to the AI Gateway tier preview regions so the model is co-located with the gateway.')
param location string = 'eastus2'

// ---------------------------------------------------------------------------
// Model deployment
// ---------------------------------------------------------------------------
@description('Model to deploy. The AI Gateway tier imports this deployment and callers reference it by this name in the request "model" field.')
param modelName string = 'gpt-5.4'

@description('Model format. Example: OpenAI.')
param modelFormat string = 'OpenAI'

@description('Model version. Ensure this version is available in your region: az cognitiveservices account list-models.')
param modelVersion string = '2026-03-05'

@description('Model deployment SKU name. Example: GlobalStandard.')
param modelSkuName string = 'GlobalStandard'

@description('Model deployment capacity in thousands of TPM. gpt-5.4 may require more than the default; set a value your subscription has quota for (the reference deployment used 681).')
param modelCapacity int = 40

// ---------------------------------------------------------------------------
// Naming
// ---------------------------------------------------------------------------
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)
var accountName = toLower('${aiServicesName}${uniqueSuffix}')

// ===========================================================================
// Foundry account (AIServices) — system-assigned identity, NO user-assigned MI.
// Local auth stays ENABLED so the gateway's key-based Foundry import works.
// ===========================================================================
resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: accountName
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
  }
}

// ===========================================================================
// Model deployment (imported by the gateway, referenced by name at call time)
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
    type: 'SystemAssigned'
  }
  properties: {
    description: projectDescription
    displayName: projectDisplayName
  }
  dependsOn: [
    modelDeployment
  ]
}

// ===========================================================================
// Outputs — use these in the AI Gateway tier "Import from Foundry" wizard
// ===========================================================================
output subscriptionId string = subscription().subscriptionId
output resourceGroupName string = resourceGroup().name
output accountName string = account.name
output accountId string = account.id
output projectName string = project.name
output modelName string = modelName
output location string = location
