/*
  ================================================================================
  main.bicep  — TC01 for the AI Gateway tier (preview)
  --------------------------------------------------------------------------------
  The gateway, model import (choose "Managed identity"), and policies are created
  in the AI Gateway tier portal (https://ai.gateway.azure.com). See README.md.

  Verified against:
    https://learn.microsoft.com/azure/api-management/ai-gateway-manage-models-tools
    https://learn.microsoft.com/azure/api-management/ai-gateway-setup
    https://learn.microsoft.com/azure/foundry/concepts/rbac-foundry
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
param projectDescription string = 'AI Gateway tier TC01 sample.'

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
// Local auth is DISABLED to enforce keyless (managed-identity) backend access.
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
    disableLocalAuth: true
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
// Outputs — use these in the AI Gateway tier "Import from Foundry" wizard.
// After you create the gateway, grant its system-assigned identity the
// Foundry User role (53ca6127-db72-4b80-b1b0-d745d6d5456d) on accountId; the
// import wizard does this automatically if you have User Access Administrator
// or Owner on the account. See README.md.
// ===========================================================================
output subscriptionId string = subscription().subscriptionId
output resourceGroupName string = resourceGroup().name
output accountName string = account.name
output accountId string = account.id
output projectName string = project.name
output modelName string = modelName
output location string = location
