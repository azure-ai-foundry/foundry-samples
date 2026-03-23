// ---- Standard Agent Setup with BYO VNet and GSA Proxy ----
// Based on 41-standard-agent-setup with the following additions:
//   - BYO VNet with agent subnet delegation (no private endpoints)
//   - GSA AI Connector proxy for egress traffic control
//   - UDR routing agent subnet 0/0 through the proxy with Azure service tag exceptions
//
// This setup keeps publicNetworkAccess: 'Enabled' on all resources since
// there are no private endpoints. The GSA proxy controls egress only.

@description('Location for all resources.')
@allowed([
  'australiaeast'
  'brazilsouth'
  'canadaeast'
  'eastus'
  'eastus2'
  'francecentral'
  'germanywestcentral'
  'italynorth'
  'japaneast'
  'koreacentral'
  'norwayeast'
  'polandcentral'
  'southafricanorth'
  'southcentralus'
  'southindia'
  'swedencentral'
  'switzerlandnorth'
  'uaenorth'
  'uksouth'
  'westeurope'
  'westus'
  'westus3'
])
param location string = 'eastus'

// ---- AI Services parameters ----
@maxLength(9)
@description('Name for your AI Services resource.')
param aiServices string = 'foundy'

@description('The name of the model you want to deploy')
param modelName string = 'gpt-4.1'
@description('The provider of your model')
param modelFormat string = 'OpenAI'
@description('The version of your model')
param modelVersion string = '2025-04-14'
@description('The sku of your model deployment')
param modelSkuName string = 'GlobalStandard'
@description('The tokens per minute (TPM) of your model deployment')
param modelCapacity int = 30

// ---- Project parameters ----
@description('Name for your project resource.')
param firstProjectName string = 'project'
@description('Description of the project')
param projectDescription string = 'AI Foundry Agent project with BYO VNet and GSA proxy'
@description('The display name of the project')
param displayName string = 'BYO VNet GSA Proxy Agent Project'
@description('The name of the project capability host')
param projectCapHost string = 'caphostproj'
@description('The name of the account capability host')
param accountCapHost string = 'caphostacc'

// ---- VNet parameters ----
@description('Name of the virtual network')
param vnetName string = 'agent-vnet'
@description('Address space for the VNet')
param vnetAddressPrefix string = '10.0.0.0/16'
@description('Name of the agent subnet')
param agentSubnetName string = 'agent-subnet'
@description('Address prefix for the agent subnet (recommended /24)')
param agentSubnetPrefix string = '10.0.0.0/24'
@description('Name of the GSA proxy subnet')
param gsaProxySubnetName string = 'gsa-proxy-subnet'
@description('Address prefix for the GSA proxy subnet')
param gsaProxySubnetPrefix string = '10.0.1.0/24'

// ---- GSA Proxy parameters ----
@description('VM size for the GSA proxy VM')
param gsaProxyVmSize string = 'Standard_D2s_v3'
@description('Admin username for the GSA proxy VM')
param gsaProxyAdminUsername string = 'azureuser'
@description('SSH public key for GSA proxy VM authentication')
@secure()
param gsaProxySshPublicKey string

// ---- Cosmos DB options ----
@description('Whether Cosmos DB should be zone redundant. Set to false in regions with limited AZ capacity (e.g. southcentralus).')
param cosmosDBZoneRedundant bool = true

// ---- Optional: bring existing resources ----
@description('The AI Search Service full ARM Resource ID. If not provided, a new one will be created.')
param aiSearchResourceId string = ''
@description('The AI Storage Account full ARM Resource ID. If not provided, a new one will be created.')
param azureStorageAccountResourceId string = ''
@description('The Cosmos DB Account full ARM Resource ID. If not provided, a new one will be created.')
param azureCosmosDBAccountResourceId string = ''

// ---- Computed variables ----
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)
var accountName = toLower('${aiServices}${uniqueSuffix}')
var projectName = toLower('${firstProjectName}${uniqueSuffix}')
var cosmosDBName = toLower('${uniqueSuffix}cosmosdb')
var aiSearchName = toLower('${uniqueSuffix}search')
var azureStorageName = toLower('${uniqueSuffix}storage')

var storagePassedIn = azureStorageAccountResourceId != ''
var searchPassedIn = aiSearchResourceId != ''
var cosmosPassedIn = azureCosmosDBAccountResourceId != ''

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
// Step 1: Create Virtual Network with Agent and GSA Proxy subnets
// ===========================================================================
module vnet 'modules/vnet.bicep' = {
  name: 'vnet-${uniqueSuffix}-deployment'
  params: {
    location: location
    vnetName: vnetName
    vnetAddressPrefix: vnetAddressPrefix
    agentSubnetName: agentSubnetName
    agentSubnetPrefix: agentSubnetPrefix
    gsaProxySubnetName: gsaProxySubnetName
    gsaProxySubnetPrefix: gsaProxySubnetPrefix
  }
}

// ===========================================================================
// Step 2: Deploy GSA Proxy (VM, NIC, NSG, UDR)
// ===========================================================================
module gsaProxy 'modules/gsa-proxy.bicep' = {
  name: 'gsa-proxy-${uniqueSuffix}-deployment'
  params: {
    location: location
    name: accountName
    vnetName: vnet.outputs.virtualNetworkName
    gsaProxySubnetId: vnet.outputs.gsaProxySubnetId
    agentSubnetName: vnet.outputs.agentSubnetName
    vmSize: gsaProxyVmSize
    adminUsername: gsaProxyAdminUsername
    sshPublicKey: gsaProxySshPublicKey
  }
}

// ===========================================================================
// Step 3: Validate existing resources
// ===========================================================================
module validateExistingResources 'modules/validate-existing-resources.bicep' = {
  name: 'validate-existing-resources-${uniqueSuffix}-deployment'
  params: {
    aiSearchResourceId: aiSearchResourceId
    azureStorageAccountResourceId: azureStorageAccountResourceId
    azureCosmosDBAccountResourceId: azureCosmosDBAccountResourceId
  }
}

// ===========================================================================
// Step 4: Create dependent resources (Cosmos DB, AI Search, Storage)
// ===========================================================================
module aiDependencies 'modules/standard-dependent-resources.bicep' = {
  name: 'dependencies-${uniqueSuffix}-deployment'
  params: {
    location: location
    azureStorageName: azureStorageName
    aiSearchName: aiSearchName
    cosmosDBName: cosmosDBName
    aiSearchResourceId: aiSearchResourceId
    aiSearchExists: validateExistingResources.outputs.aiSearchExists
    azureStorageAccountResourceId: azureStorageAccountResourceId
    azureStorageExists: validateExistingResources.outputs.azureStorageExists
    cosmosDBResourceId: azureCosmosDBAccountResourceId
    cosmosDBExists: validateExistingResources.outputs.cosmosDBExists
    cosmosDBZoneRedundant: cosmosDBZoneRedundant
  }
}

// ===========================================================================
// Step 5: Create AI Services account with BYO VNet subnet injection
// ===========================================================================
module aiAccount 'modules/ai-account-identity.bicep' = {
  name: 'ai-${accountName}-${uniqueSuffix}-deployment'
  params: {
    accountName: accountName
    location: location
    modelName: modelName
    modelFormat: modelFormat
    modelVersion: modelVersion
    modelSkuName: modelSkuName
    modelCapacity: modelCapacity
    agentSubnetId: vnet.outputs.agentSubnetId
  }
  dependsOn: [
    validateExistingResources
    aiDependencies
    gsaProxy // Ensure UDR is in place before AI account uses the subnet
  ]
}

// ===========================================================================
// Step 6: Create AI Project
// ===========================================================================
module aiProject 'modules/ai-project-identity.bicep' = {
  name: 'ai-${projectName}-${uniqueSuffix}-deployment'
  params: {
    projectName: projectName
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
}

module formatProjectWorkspaceId 'modules/format-project-workspace-id.bicep' = {
  name: 'format-project-workspace-id-${uniqueSuffix}-deployment'
  params: {
    projectWorkspaceId: aiProject.outputs.projectWorkspaceId
  }
}

// ===========================================================================
// Step 7: Role assignments
// ===========================================================================
module storageAccountRoleAssignment 'modules/azure-storage-account-role-assignment.bicep' = {
  name: 'storage-${azureStorageName}-${uniqueSuffix}-deployment'
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
  params: {
    azureStorageName: aiDependencies.outputs.azureStorageName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
}

module cosmosAccountRoleAssignments 'modules/cosmosdb-account-role-assignment.bicep' = {
  name: 'cosmos-account-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
  params: {
    cosmosDBName: aiDependencies.outputs.cosmosDBName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    storageAccountRoleAssignment
  ]
}

module aiSearchRoleAssignments 'modules/ai-search-role-assignments.bicep' = {
  name: 'ai-search-ra-${uniqueSuffix}-deployment'
  scope: resourceGroup(aiSearchServiceSubscriptionId, aiSearchServiceResourceGroupName)
  params: {
    aiSearchName: aiDependencies.outputs.aiSearchName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    cosmosAccountRoleAssignments
    storageAccountRoleAssignment
  ]
}

// ===========================================================================
// Step 8: Create capability host
// ===========================================================================
module addProjectCapabilityHost 'modules/add-project-capability-host.bicep' = {
  name: 'capabilityHost-configuration-${uniqueSuffix}-deployment'
  params: {
    accountName: aiAccount.outputs.accountName
    projectName: aiProject.outputs.projectName
    cosmosDBConnection: aiProject.outputs.cosmosDBConnection
    azureStorageConnection: aiProject.outputs.azureStorageConnection
    aiSearchConnection: aiProject.outputs.aiSearchConnection
    projectCapHost: projectCapHost
    accountCapHost: accountCapHost
  }
  dependsOn: [
    aiSearchRoleAssignments
    cosmosAccountRoleAssignments
    storageAccountRoleAssignment
  ]
}

// ===========================================================================
// Step 9: Post-caphost role assignments
// ===========================================================================
module storageContainersRoleAssignment 'modules/blob-storage-container-role-assignments.bicep' = {
  name: 'storage-containers-${uniqueSuffix}-deployment'
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

module cosmosContainerRoleAssignments 'modules/cosmos-container-role-assignments.bicep' = {
  name: 'cosmos-ra-${uniqueSuffix}-deployment'
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
