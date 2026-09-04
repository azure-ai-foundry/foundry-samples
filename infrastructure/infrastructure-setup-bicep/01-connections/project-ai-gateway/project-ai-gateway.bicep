/*
  Create a new Azure AI Foundry project under an EXISTING (already AI-Gateway-enabled)
  Foundry account and enable the AI Gateway on the new project by default.

  This sample provisions the per-project API Management (APIM) artifacts that make a
  project "gateway enabled":

    1. Create the project (Microsoft.CognitiveServices/accounts/projects).
    2. Provision the per-project APIM artifacts on the account's gateway:
         a. an APIM Product (subscriptionRequired: true, state: 'published'),
         b. an association of that Product to the gateway's shared API
            (the shared API is named after the Foundry account),
         c. an active APIM Subscription scoped to the Product,
         d. an ARM resource link from the project -> the Product. The presence of this
            link is what marks the project as Enabled on the gateway.

  The parent account is already "gateway enabled" because it has an ARM resource link
  whose target is an APIM service. Pass that APIM service resource id as apimResourceId.

  Prerequisites:
    - An existing Azure AI Foundry (Cognitive Services) account that already fronts an
      AI Gateway (i.e. an APIM service).
    - The APIM service that backs the account gateway, and its full ARM resource id.

  IMPORTANT: Make sure you are logged into the subscription where the AI Foundry account
  exists before deploying. Use: az account set --subscription <foundry-subscription-id>

  Run command (deploy into the resource group / subscription where the account lives):
    az deployment group create \
      --resource-group <foundry-rg> \
      --template-file project-ai-gateway.bicep \
      --parameters @samples/parameters.json
*/

targetScope = 'resourceGroup'

// ========================================
// REQUIRED PARAMETERS
// ========================================

@description('Name of the EXISTING Foundry (Cognitive Services) account that already fronts an AI Gateway.')
param aiFoundryAccountName string = 'sample-foundry-account'

@description('Name of the NEW project to create under the account.')
param projectName string = 'sample-project'

@description('Full ARM resource ID of the APIM service that backs the account AI Gateway (the target of the account APIM resource link).')
param apimResourceId string = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/sample-rg/providers/Microsoft.ApiManagement/service/sample-apim'

// ========================================
// OPTIONAL PARAMETERS
// ========================================

@description('Display name for the new project. Defaults to projectName.')
param projectDisplayName string = projectName

@description('Description for the new project.')
param projectDescription string = 'Project created with AI Gateway enabled by default.'

@description('Location for the new project. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Id of the gateway shared API in APIM to associate the product with. The gateway exposes one shared API named after the account, so this defaults to the account name.')
param sharedApiId string = ''

// ========================================
// DERIVED VALUES
// ========================================

var apimSubscriptionId = split(apimResourceId, '/')[2]
var apimResourceGroupName = split(apimResourceId, '/')[4]
var apimServiceName = split(apimResourceId, '/')[8]

// The gateway shared API is named after the Foundry account.
var effectiveSharedApiId = empty(sharedApiId) ? toLower(aiFoundryAccountName) : sharedApiId

// Per-project APIM entity name: {account}-{project}-ai-{unique}, lowercased.
var productName = toLower('${take(aiFoundryAccountName, 24)}-${take(projectName, 24)}-ai-${uniqueString(subscription().id, resourceGroup().id, aiFoundryAccountName, projectName)}')

// Resource-link target: the raw APIM service id + /products/{productName}.
var productScope = '${apimResourceId}/products/${productName}'

// ========================================
// 1. NEW PROJECT
// ========================================

@description('The existing, already gateway-enabled Foundry account (parent).')
resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: aiFoundryAccountName
}

@description('The newly created project under the existing Foundry account.')
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: aiFoundry
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectDisplayName
    description: projectDescription
  }
}

// ========================================
// 2. PER-PROJECT APIM ARTIFACTS (product + api association + subscription)
//    Created in the APIM service resource group / subscription.
// ========================================

module gatewayArtifacts 'modules/apim-gateway-artifacts.bicep' = {
  name: 'apim-gateway-artifacts-${uniqueString(productName)}'
  scope: resourceGroup(apimSubscriptionId, apimResourceGroupName)
  params: {
    apimServiceName: apimServiceName
    productName: productName
    sharedApiId: effectiveSharedApiId
  }
}

// ========================================
// 3. PROJECT -> PRODUCT RESOURCE LINK (drives gateway Enabled status)
// ========================================

@description('ARM resource link from the project to the per-project APIM product. Its presence marks the project as Enabled on the gateway.')
resource gatewayLink 'Microsoft.Resources/links@2016-09-01' = {
  scope: project
  name: 'ai-gateway-${productName}'
  properties: {
    targetId: productScope
    notes: 'Enables the project on the account AI Gateway (project -> APIM product).'
  }
  dependsOn: [
    gatewayArtifacts
  ]
}

// ========================================
// OUTPUTS
// ========================================

@description('Resource ID of the newly created project.')
output projectResourceId string = project.id

@description('System-assigned principal ID of the new project.')
output projectPrincipalId string = project.identity.principalId

@description('Name of the per-project APIM product created on the gateway.')
output productName string = productName

@description('Resource-link target (the APIM product) that marks the project Enabled.')
output gatewayProductScope string = productScope
