/*
  ================================================================================
  Template 16 extension (byom-cross-region): Private Cross-Region BYOM with Azure API Management
  --------------------------------------------------------------------------------
  Extends template 16 (private-network standard agent + APIM PE) by adding:

    * An APIM service (StandardV2, outbound VNet-integrated)
    * A backend Foundry account in a SECOND region, publicNetworkAccess=Disabled
    * gpt-4o / gpt-5 / gpt-5.1 model deployments on the backend account
    * A backend-pe subnet on the project VNet
    * A cross-region private endpoint into the backend account
    * The /inference API on APIM + full MI-token + backend-rewrite policy chain
    * A role assignment so APIM's MI can mint tokens for the backend account
    * A BYOM model connection on the project pointing at APIM
      (calls ../../../01-connections/apim/connection-apim.bicep)

  Includes ALL of template 16's modules end-to-end (VNet, AI account + project,
  dependent resources, private endpoints, RBAC chain, capability host) so this
  template stands alone — no chained deployment of template 16 required.

  Reference: https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway
  ================================================================================
*/

// ---------------------------------------------------------------------------
// Project-region inputs (these mirror template 16's main.bicep parameters)
// ---------------------------------------------------------------------------

@description('Azure region for the project, VNet, and APIM. Example: canadaeast.')
param location string = 'canadaeast'

@description('Name for the project-region AI Services / Foundry account.')
param aiServices string = 'aiservices'

@description('Model deployed on the project Foundry account (covers local agent needs and fallback).')
param projectModelName string = 'gpt-4o'
param projectModelFormat string = 'OpenAI'
param projectModelVersion string = '2024-11-20'
param projectModelSkuName string = 'GlobalStandard'
param projectModelCapacity int = 30

param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)
var accountName = toLower('${aiServices}${uniqueSuffix}')

@description('First Foundry project name (base; suffix appended at deploy time).')
param firstProjectName string = 'project'

@description('Description and display name for the first project.')
param projectDescription string = 'Cross-region private BYOM via APIM'
param displayName string = 'cross-region BYOM project'

// ---------------------------------------------------------------------------
// VNet inputs
// ---------------------------------------------------------------------------
@description('Name for the VNet (created if missing).')
param vnetName string = 'agent-vnet-test'

@description('Existing VNet resource ID if reusing. Leave empty to create a new VNet.')
param existingVnetResourceId string = ''

@description('Address space for the VNet (only used when creating a new VNet).')
param vnetAddressPrefix string = '192.168.0.0/16'

param agentSubnetName string = 'agent-subnet'
param agentSubnetPrefix string = ''
param peSubnetName string = 'pe-subnet'
param peSubnetPrefix string = ''

@description('Subnet CIDR for the cross-region PE into the backend Foundry account. Must be a /27 or larger inside vnetAddressPrefix.')
param backendPeSubnetName string = 'backend-pe'
param backendPeSubnetPrefix string = '192.168.3.0/27'

@description('Subnet CIDR for APIM SV2 outbound VNet integration. Must be a /27 or larger.')
param apimOutboundSubnetName string = 'apim-outbound'
param apimOutboundSubnetPrefix string = '192.168.2.0/27'

// ---------------------------------------------------------------------------
// BYO dependency inputs (reused as-is from template 16)
// ---------------------------------------------------------------------------
param aiSearchResourceId string = ''
param azureStorageAccountResourceId string = ''
param azureCosmosDBAccountResourceId string = ''

@description('Existing APIM service resource ID. Leave empty to let this template create a new StandardV2 APIM.')
param apiManagementResourceId string = ''

// ---------------------------------------------------------------------------
// APIM inputs (only used when creating a new APIM)
// ---------------------------------------------------------------------------
@description('Name for the new APIM service. Globally unique. Ignored if apiManagementResourceId is provided. Leave empty to auto-generate.')
param apimName string = ''

@description('Publisher email required by APIM at create time.')
param publisherEmail string

@description('Publisher organization name required by APIM at create time.')
param publisherName string

// ---------------------------------------------------------------------------
// Backend (second-region) inputs
// ---------------------------------------------------------------------------
@description('Region for the backend Foundry account. Must differ from location. Example: japaneast.')
param backendLocation string

@description('Globally unique name for the backend Foundry account. 2-64 chars, no dots.')
param backendAccountName string

@description('Model deployments to create on the backend account. Each entry: { name, format, version, skuName, capacity }.')
param backendModelDeployments array = [
  {
    name: 'gpt-4o'
    format: 'OpenAI'
    version: '2024-11-20'
    skuName: 'GlobalStandard'
    capacity: 10
  }
  {
    name: 'gpt-5'
    format: 'OpenAI'
    version: '2025-08-07'
    skuName: 'GlobalStandard'
    capacity: 10
  }
  {
    name: 'gpt-5.1'
    format: 'OpenAI'
    version: '2025-11-13'
    skuName: 'GlobalStandard'
    capacity: 10
  }
]

// ---------------------------------------------------------------------------
// BYOM connection inputs
// ---------------------------------------------------------------------------
@description('Foundry portal name for the AI Gateway connection. Shows up as <connectionName>/<deploymentName> in agent code.')
param connectionName string = 'ai-gateway'

@description('Inference API version sent to the backend by Foundry SDK calls.')
param inferenceApiVersion string = '2024-10-21'

@description('Application (client) ID of the Foundry project managed identity. APIM uses this to validate inbound tokens.')
param projectMiClientId string

// ---------------------------------------------------------------------------
// DNS zones (same shape as template 16)
// ---------------------------------------------------------------------------
param existingDnsZones object = {
  'privatelink.services.ai.azure.com': ''
  'privatelink.openai.azure.com': ''
  'privatelink.cognitiveservices.azure.com': ''
  'privatelink.search.windows.net': ''
  'privatelink.blob.core.windows.net': ''
  'privatelink.documents.azure.com': ''
  'privatelink.azure-api.net': ''
}

param dnsZoneNames array = [
  'privatelink.services.ai.azure.com'
  'privatelink.openai.azure.com'
  'privatelink.cognitiveservices.azure.com'
  'privatelink.search.windows.net'
  'privatelink.blob.core.windows.net'
  'privatelink.documents.azure.com'
  'privatelink.azure-api.net'
]

@description('Project capability host name.')
param projectCapHost string = 'caphostproj'

// ===========================================================================
// Variables: split BYO resource IDs into (sub, rg, name) tuples
// ===========================================================================
var storagePassedIn = !empty(azureStorageAccountResourceId)
var searchPassedIn = !empty(aiSearchResourceId)
var cosmosPassedIn = !empty(azureCosmosDBAccountResourceId)

var acsParts = split(aiSearchResourceId, '/')
var aiSearchServiceSubscriptionId = searchPassedIn ? acsParts[2] : subscription().subscriptionId
var aiSearchServiceResourceGroupName = searchPassedIn ? acsParts[4] : resourceGroup().name

var cosmosParts = split(azureCosmosDBAccountResourceId, '/')
var cosmosDBSubscriptionId = cosmosPassedIn ? cosmosParts[2] : subscription().subscriptionId
var cosmosDBResourceGroupName = cosmosPassedIn ? cosmosParts[4] : resourceGroup().name

var storageParts = split(azureStorageAccountResourceId, '/')
var azureStorageSubscriptionId = storagePassedIn ? storageParts[2] : subscription().subscriptionId
var azureStorageResourceGroupName = storagePassedIn ? storageParts[4] : resourceGroup().name

// ===========================================================================
// Compose: VNet (template 16 base) + backend-pe subnet + apim-outbound subnet
// ===========================================================================
module vnet 'modules/vnet-with-backend-subnet.bicep' = {
  name: 'vnet-${vnetName}-deployment'
  params: {
    location: location
    vnetName: vnetName
    useExistingVnet: !empty(existingVnetResourceId)
    vnetAddressPrefix: vnetAddressPrefix
    agentSubnetName: agentSubnetName
    agentSubnetPrefix: agentSubnetPrefix
    peSubnetName: peSubnetName
    peSubnetPrefix: peSubnetPrefix
    backendPeSubnetName: backendPeSubnetName
    backendPeSubnetPrefix: backendPeSubnetPrefix
  }
}

// Add the apim-outbound subnet alongside the others. Separate resource to
// avoid racing the vnet module on subnet collection writes.
resource apimOutboundSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  name: '${vnetName}/${apimOutboundSubnetName}'
  properties: {
    addressPrefix: apimOutboundSubnetPrefix
    // defaultOutboundAccess: false closes the implicit egress path Azure
    // assigns to subnets that lack explicit outbound (NAT GW / LB).
    // APIM's outbound traffic is brokered by the SV2 platform, so the
    // subnet itself does not need default outbound — and disabling it
    // also satisfies subscription-level guardrails that require
    // defaultOutboundAccess=false on every subnet.
    defaultOutboundAccess: false
    delegations: [
      {
        name: 'Microsoft.Web/serverFarms'
        properties: {
          serviceName: 'Microsoft.Web/serverFarms'
        }
      }
    ]
  }
  dependsOn: [
    vnet
  ]
}

// ===========================================================================
// Project-region Foundry account + project (delegated to template 16 modules)
// ===========================================================================
module aiAccount '../../modules-network-secured/ai-account-identity.bicep' = {
  name: 'ai-${accountName}-deployment'
  params: {
    accountName: accountName
    location: location
    modelName: projectModelName
    modelFormat: projectModelFormat
    modelVersion: projectModelVersion
    modelSkuName: projectModelSkuName
    modelCapacity: projectModelCapacity
    agentSubnetId: vnet.outputs.agentSubnetId
  }
}

module validateExistingResources '../../modules-network-secured/validate-existing-resources.bicep' = {
  name: 'validate-${uniqueSuffix}-deployment'
  params: {
    aiSearchResourceId: aiSearchResourceId
    azureStorageAccountResourceId: azureStorageAccountResourceId
    azureCosmosDBAccountResourceId: azureCosmosDBAccountResourceId
    apiManagementResourceId: apiManagementResourceId
    existingDnsZones: existingDnsZones
    dnsZoneNames: dnsZoneNames
  }
}

module aiDependencies '../../modules-network-secured/standard-dependent-resources.bicep' = {
  name: 'dependencies-${uniqueSuffix}-deployment'
  params: {
    location: location
    azureStorageName: toLower('${aiServices}${uniqueSuffix}storage')
    aiSearchName: toLower('${aiServices}${uniqueSuffix}search')
    cosmosDBName: toLower('${aiServices}${uniqueSuffix}cosmosdb')
    aiSearchResourceId: aiSearchResourceId
    aiSearchExists: validateExistingResources.outputs.aiSearchExists
    azureStorageAccountResourceId: azureStorageAccountResourceId
    azureStorageExists: validateExistingResources.outputs.azureStorageExists
    cosmosDBResourceId: azureCosmosDBAccountResourceId
    cosmosDBExists: validateExistingResources.outputs.cosmosDBExists
  }
}

// Existing-resource references so subsequent modules can dependsOn them
resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: aiDependencies.outputs.azureStorageName
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
}

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' existing = {
  name: aiDependencies.outputs.aiSearchName
  scope: resourceGroup(aiDependencies.outputs.aiSearchServiceSubscriptionId, aiDependencies.outputs.aiSearchServiceResourceGroupName)
}

resource cosmosDB 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: aiDependencies.outputs.cosmosDBName
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
}

// ===========================================================================
// Private endpoints + DNS zones for project-region resources
// (Storage, Cosmos, AI Search, project Foundry account, optionally APIM)
// ===========================================================================
module privateEndpointAndDNS '../../modules-network-secured/private-endpoint-and-dns.bicep' = {
  name: 'pe-and-dns-${uniqueSuffix}-deployment'
  params: {
    aiAccountName: aiAccount.outputs.accountName
    aiSearchName: aiDependencies.outputs.aiSearchName
    storageName: aiDependencies.outputs.azureStorageName
    cosmosDBName: aiDependencies.outputs.cosmosDBName
    apiManagementName: validateExistingResources.outputs.apiManagementName
    vnetName: vnet.outputs.virtualNetworkName
    peSubnetName: vnet.outputs.peSubnetName
    suffix: uniqueSuffix
    vnetResourceGroupName: vnet.outputs.virtualNetworkResourceGroup
    vnetSubscriptionId: vnet.outputs.virtualNetworkSubscriptionId
    cosmosDBSubscriptionId: cosmosDBSubscriptionId
    cosmosDBResourceGroupName: cosmosDBResourceGroupName
    aiSearchSubscriptionId: aiSearchServiceSubscriptionId
    aiSearchResourceGroupName: aiSearchServiceResourceGroupName
    storageAccountResourceGroupName: azureStorageResourceGroupName
    storageAccountSubscriptionId: azureStorageSubscriptionId
    apiManagementResourceGroupName: validateExistingResources.outputs.apiManagementResourceGroupName
    apiManagementSubscriptionId: validateExistingResources.outputs.apiManagementSubscriptionId
    existingDnsZones: existingDnsZones
  }
  dependsOn: [
    aiSearch
    storage
    cosmosDB
  ]
}

// ===========================================================================
// APIM service (skipped if reusing an existing APIM)
// ===========================================================================
var shouldCreateApim = empty(apiManagementResourceId)

module apimService 'modules/apim-service.bicep' = if (shouldCreateApim) {
  name: 'apim-${uniqueSuffix}-deployment'
  params: {
    location: location
    apimName: empty(apimName) ? 'apim-${uniqueSuffix}-aigw' : apimName
    apimOutboundSubnetId: apimOutboundSubnet.id
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

var apimIdParts = empty(apiManagementResourceId)
  ? ['', '', subscription().subscriptionId, '', resourceGroup().name, '', '', '', 'placeholder']
  : split(apiManagementResourceId, '/')

resource existingApim 'Microsoft.ApiManagement/service@2024-05-01' existing = if (!shouldCreateApim) {
  name: apimIdParts[8]
  scope: resourceGroup(apimIdParts[2], apimIdParts[4])
}

var effectiveApimName = shouldCreateApim ? apimService!.outputs.apimName : apimIdParts[8]
var effectiveApimResourceId = shouldCreateApim ? apimService!.outputs.apimResourceId : apiManagementResourceId
var effectiveApimPrincipalId = shouldCreateApim ? apimService!.outputs.apimPrincipalId : existingApim.identity.principalId

// ===========================================================================
// Backend Foundry account in the backend region + cross-region private endpoint
// ===========================================================================
module backendAccount 'modules/backend-ai-account.bicep' = {
  name: 'backend-${backendAccountName}-deployment'
  params: {
    location: backendLocation
    accountName: backendAccountName
    modelDeployments: backendModelDeployments
  }
}

// Reference the private DNS zones that template 16 deploys (or that we
// reused if they pre-existed). They live in the current resource group
// because that's where standard-dependent-resources.bicep / private-
// endpoint-and-dns.bicep create them.
resource cognitiveServicesDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' existing = {
  name: 'privatelink.cognitiveservices.azure.com'
}

resource openAiDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' existing = {
  name: 'privatelink.openai.azure.com'
}

resource servicesAiDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' existing = {
  name: 'privatelink.services.ai.azure.com'
}

module backendPe 'modules/backend-private-endpoint.bicep' = {
  name: 'backend-pe-${uniqueSuffix}-deployment'
  params: {
    location: location
    backendAccountResourceId: backendAccount.outputs.accountId
    backendPeSubnetId: vnet.outputs.backendPeSubnetId
    suffix: uniqueSuffix
    cognitiveServicesDnsZoneId: cognitiveServicesDnsZone.id
    openAiDnsZoneId: openAiDnsZone.id
    servicesAiDnsZoneId: servicesAiDnsZone.id
  }
  dependsOn: [
    aiDependencies
  ]
}

// ===========================================================================
// Grant APIM's MI access to the backend Foundry account
// ===========================================================================
module apimBackendRole 'modules/apim-backend-role-assignment.bicep' = {
  name: 'apim-backend-role-${uniqueSuffix}-deployment'
  params: {
    apimPrincipalId: effectiveApimPrincipalId
    backendAccountName: backendAccount.outputs.accountName
  }
}

// ===========================================================================
// /inference API on APIM with the full MI + backend-rewrite policy chain
// ===========================================================================
module inferenceApi 'modules/apim-inference-api.bicep' = {
  name: 'inference-api-${uniqueSuffix}-deployment'
  params: {
    apimName: effectiveApimName
    projectMiClientId: projectMiClientId
    backendAccountName: backendAccount.outputs.accountName
    backendRegion: backendLocation
    projectRegion: location
  }
  dependsOn: [
    apimBackendRole
    backendPe
  ]
}

// ===========================================================================
// Project + BYO dependencies wiring (delegated to template 16 modules)
// ===========================================================================
module aiProject '../../modules-network-secured/ai-project-identity.bicep' = {
  name: 'project-${uniqueSuffix}-deployment'
  params: {
    projectName: toLower('${firstProjectName}${uniqueSuffix}')
    projectDescription: projectDescription
    displayName: displayName
    location: location
    aiSearchName: aiDependencies.outputs.aiSearchName
    aiSearchServiceResourceGroupName: aiDependencies.outputs.aiSearchServiceResourceGroupName
    aiSearchServiceSubscriptionId: aiDependencies.outputs.aiSearchServiceSubscriptionId
    cosmosDBName: aiDependencies.outputs.cosmosDBName
    cosmosDBSubscriptionId: aiDependencies.outputs.cosmosDBSubscriptionId
    cosmosDBResourceGroupName: aiDependencies.outputs.cosmosDBResourceGroupName
    azureStorageName: aiDependencies.outputs.azureStorageName
    azureStorageSubscriptionId: aiDependencies.outputs.azureStorageSubscriptionId
    azureStorageResourceGroupName: aiDependencies.outputs.azureStorageResourceGroupName
    accountName: aiAccount.outputs.accountName
  }
  dependsOn: [
    privateEndpointAndDNS
    cosmosDB
    aiSearch
    storage
  ]
}

// ===========================================================================
// Project RBAC + capability host (template 16 modules, ordered as 16 does)
// ===========================================================================
module formatProjectWorkspaceId '../../modules-network-secured/format-project-workspace-id.bicep' = {
  name: 'format-project-workspace-id-${uniqueSuffix}-deployment'
  params: {
    projectWorkspaceId: aiProject.outputs.projectWorkspaceId
  }
}

// Account-level RBAC: must be assigned BEFORE the capability host is created
module storageAccountRoleAssignment '../../modules-network-secured/azure-storage-account-role-assignment.bicep' = {
  name: 'storage-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
  params: {
    azureStorageName: aiDependencies.outputs.azureStorageName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    storage
    privateEndpointAndDNS
  ]
}

module cosmosAccountRoleAssignments '../../modules-network-secured/cosmosdb-account-role-assignment.bicep' = {
  name: 'cosmos-account-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
  params: {
    cosmosDBName: aiDependencies.outputs.cosmosDBName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    cosmosDB
    privateEndpointAndDNS
  ]
}

module aiSearchRoleAssignments '../../modules-network-secured/ai-search-role-assignments.bicep' = {
  name: 'ai-search-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(aiSearchServiceSubscriptionId, aiSearchServiceResourceGroupName)
  params: {
    aiSearchName: aiDependencies.outputs.aiSearchName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    aiSearch
    privateEndpointAndDNS
  ]
}

module addProjectCapabilityHost '../../modules-network-secured/add-project-capability-host.bicep' = {
  name: 'capabilityHost-${uniqueSuffix}-deployment'
  params: {
    accountName: aiAccount.outputs.accountName
    projectName: aiProject.outputs.projectName
    cosmosDBConnection: aiProject.outputs.cosmosDBConnection
    azureStorageConnection: aiProject.outputs.azureStorageConnection
    aiSearchConnection: aiProject.outputs.aiSearchConnection
    projectCapHost: projectCapHost
  }
  dependsOn: [
    aiSearch
    storage
    cosmosDB
    privateEndpointAndDNS
    cosmosAccountRoleAssignments
    storageAccountRoleAssignment
    aiSearchRoleAssignments
  ]
}

// Container-level RBAC: must be assigned AFTER the capability host is created
module storageContainersRoleAssignment '../../modules-network-secured/blob-storage-container-role-assignments.bicep' = {
  name: 'storage-containers-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
  params: {
    aiProjectPrincipalId: aiProject.outputs.projectPrincipalId
    storageName: aiDependencies.outputs.azureStorageName
    workspaceId: formatProjectWorkspaceId.outputs.projectWorkspaceIdGuid
  }
  dependsOn: [
    addProjectCapabilityHost
  ]
}

module cosmosContainerRoleAssignments '../../modules-network-secured/cosmos-container-role-assignments.bicep' = {
  name: 'cosmos-container-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
  params: {
    cosmosAccountName: aiDependencies.outputs.cosmosDBName
    projectWorkspaceId: formatProjectWorkspaceId.outputs.projectWorkspaceIdGuid
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    addProjectCapabilityHost
    storageContainersRoleAssignment
  ]
}

// ===========================================================================
// BYOM model connection on the project, pointing at APIM
// (calls the canonical 01-connections/apim/connection-apim.bicep module)
// ===========================================================================
module byomConnection '../../../01-connections/apim/connection-apim.bicep' = {
  name: 'byom-connection-${uniqueSuffix}-deployment'
  params: {
    projectResourceId: aiProject.outputs.projectId
    apimResourceId: effectiveApimResourceId
    apiName: inferenceApi.outputs.apiName
    connectionName: connectionName
    authType: 'ProjectManagedIdentity'
    isSharedToAll: true
    deploymentInPath: 'true'
    inferenceAPIVersion: inferenceApiVersion
    staticModels: [for d in backendModelDeployments: {
      name: d.name
      properties: {
        model: {
          name: d.name
          version: d.version
          format: d.format
        }
      }
    }]
  }
  dependsOn: [
    addProjectCapabilityHost
  ]
}

// ===========================================================================
// Outputs
// ===========================================================================
output projectName string = aiProject.outputs.projectName
output projectId string = aiProject.outputs.projectId
output apimGatewayUrl string = shouldCreateApim ? apimService!.outputs.apimGatewayUrl : 'https://${effectiveApimName}.azure-api.net'
output backendAccountId string = backendAccount.outputs.accountId
output byomConnectionName string = byomConnection.outputs.connectionName
output backendPrivateEndpointId string = backendPe.outputs.privateEndpointId
