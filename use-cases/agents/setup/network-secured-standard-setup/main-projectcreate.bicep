// Standard agent setup 

@description('Resource group location')
param resourceGroupLocation string = resourceGroup().location

@allowed([
    'australiaeast'
    'eastus'
    'eastus2'
    'francecentral'
    'japaneast'
    'norwayeast'
    'southindia'
    'swedencentral'
    'uaenorth'
    'uksouth'
    'westus'
    'westus3'
  ])
@description('Location for all resources.')
param location string = resourceGroupLocation

@description('Name for your AI Services resource.')
param aiServices string = 'aiservices'

@description('Name for your project resource.')
param firstProjectName string = 'project'

@description('This project will be a sub-resource of your account')
param projectDescription string = 'A project for the AI Foundry account with network secured deployed Agent'

@description('The display name of the project')
param displayName string = 'project'

//Existing standard Agent required resources
@description('The AI Search Service full ARM Resource ID. This is an optional field, and if not provided, the resource will be created.')
param aiSearchResourceId string = '/subscriptions/562da9fc-fd6e-4f24-a6aa-99827a7f6f91/resourceGroups/rg-vpn-fdp-ni-eus/providers/Microsoft.Search/searchServices/aiservicesgckssearch'
@description('The AI Storage Account full ARM Resource ID. This is an optional field, and if not provided, the resource will be created.')
param azureStorageAccountResourceId string = '/subscriptions/562da9fc-fd6e-4f24-a6aa-99827a7f6f91/resourceGroups/rg-vpn-fdp-ni-eus/providers/Microsoft.Storage/storageAccounts/aiservicesgcksstorage'
@description('The Cosmos DB Account full ARM Resource ID. This is an optional field, and if not provided, the resource will be created.')
param azureCosmosDBAccountResourceId string = '/subscriptions/562da9fc-fd6e-4f24-a6aa-99827a7f6f91/resourceGroups/rg-vpn-fdp-ni-eus/providers/Microsoft.DocumentDB/databaseAccounts/aiservicesgckscosmosdb'

param projectCapHost string = 'caphostproj'


// Create a short, unique suffix, that will be unique to each resource group
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)
var accountName = toLower('${aiServices}${uniqueSuffix}')
var projectName = toLower('${firstProjectName}${uniqueSuffix}')


var cosmosDBName = toLower('${aiServices}${uniqueSuffix}cosmosdb')
var aiSearchName = toLower('${aiServices}${uniqueSuffix}search')
var azureStorageName = toLower('${aiServices}${uniqueSuffix}storage')

// Check if existing resources have been passed in
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

/*
  Validate existing resources
  This module will check if the AI Search Service, Storage Account, and Cosmos DB Account already exist.
  If they do, it will set the corresponding output to true. If they do not exist, it will set the output to false.
*/
module validateExistingResources 'modules-network-secured/validate-existing-resources.bicep' = {
  name: 'validate-existing-resources-${uniqueSuffix}-deployment'
  params: {
    aiSearchResourceId: aiSearchResourceId
    azureStorageAccountResourceId: azureStorageAccountResourceId
    azureCosmosDBAccountResourceId: azureCosmosDBAccountResourceId
  }
}

// This module will create new agent dependent resources
// A Cosmos DB account, an AI Search Service, and a Storage Account are created if they do not already exist
module aiDependencies 'modules-network-secured/standard-dependent-resources.bicep' = {
  name: 'dependencies-${accountName}-${uniqueSuffix}-deployment'
  params: {
    location: location
    azureStorageName: azureStorageName
    aiSearchName: aiSearchName
    cosmosDBName: cosmosDBName
    
    // AI Search Service parameters
    aiSearchResourceId: aiSearchResourceId
    aiSearchExists: validateExistingResources.outputs.aiSearchExists

    // Storage Account
    azureStorageAccountResourceId: azureStorageAccountResourceId
    azureStorageExists: validateExistingResources.outputs.azureStorageExists

    // Cosmos DB Account
    cosmosDBResourceId: azureCosmosDBAccountResourceId
    cosmosDBExists: validateExistingResources.outputs.cosmosDBExists
    }
}

/*
  Create the AI Services account and gpt-4o model deployment
*/
module aiAccount 'modules-network-secured/ai-account-reference.bicep' = {
  name: 'ai-${accountName}-${uniqueSuffix}-deployment'
  params: {
    // workspace organization
    accountName: accountName
  }
  dependsOn: [
    validateExistingResources, aiDependencies
  ]
}

/*
  Creates a new project (sub-resource of the AI Services account)
*/
module aiProject 'modules-network-secured/ai-project-identity.bicep' = {
  name: 'ai-${projectName}-${uniqueSuffix}-deployment'
  params: {
    // workspace organization
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
    // dependent resources
    accountName: aiAccount.outputs.accountName
  }
}



// Private Endpoint and DNS Configuration
// This module sets up private network access for all Azure services:
// 1. Creates private endpoints in the specified subnet
// 2. Sets up private DNS zones for each service:
//    - privatelink.search.windows.net for AI Search
//    - privatelink.cognitiveservices.azure.com for AI Services
//    - privatelink.blob.core.windows.net for Storage
// 3. Links private DNS zones to the VNet for name resolution
// 4. Configures network policies to restrict access to private endpoints only
module privateEndpointAndDNS 'modules-network-secured/private-endpoint-and-dns.bicep' = {
    name: '${uniqueSuffix}-private-endpoint'
    params: {
      aiAccountName: aiAccount.outputs.accountName    // AI Services to secure
      aiSearchName: aiDependencies.outputs.aiSearchName       // AI Search to secure
      storageName: aiDependencies.outputs.azureStorageName        // Storage to secure
      cosmosDBName:aiDependencies.outputs.cosmosDBName
      vnetName: vnet.outputs.virtualNetworkName    // VNet containing subnets
      peSubnetName: vnet.outputs.peSubnetName        // Subnet for private endpoints
      suffix: uniqueSuffix                                    // Unique identifier
    }
  }


/*
  Assigns the project SMI the storage blob data contributor role on the storage account
*/
module storageAccountRoleAssignment 'modules-network-secured/azure-storage-account-role-assignment.bicep' = {
  name: 'storage-${azureStorageName}-${uniqueSuffix}-deployment'
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
  params: { 
    accountPrincipalId: aiAccount.outputs.accountPrincipalId
    azureStorageName: aiDependencies.outputs.azureStorageName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
}

// The Comos DB Operator role must be assigned before the caphost is created
module cosmosAccountRoleAssignments 'modules-network-secured/cosmosdb-account-role-assignment.bicep' = {
  name: 'cosmos-account-role-assignments-${projectName}-${uniqueSuffix}-deployment'
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
  params: {
    cosmosDBName: aiDependencies.outputs.cosmosDBName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn: [
    storageAccountRoleAssignment
  ]

}

// This role can be assigned before or after the caphost is created
module aiSearchRoleAssignments 'modules-network-secured/ai-search-role-assignments.bicep' = {
  name: 'ai-search-role-assignments-${projectName}-${uniqueSuffix}-deployment'
  scope: resourceGroup(aiSearchServiceSubscriptionId, aiSearchServiceResourceGroupName)
  params: {
    aiSearchName: aiDependencies.outputs.aiSearchName
    projectPrincipalId: aiProject.outputs.projectPrincipalId
  }
  dependsOn:[
    cosmosAccountRoleAssignments, storageAccountRoleAssignment
  ]
}

// This module creates the capability host for the project and account
module addProjectCapabilityHost 'modules-network-secured/add-project-capability-host.bicep' = {
  name: 'capabilityHost-configuration-${projectName}-${uniqueSuffix}-deployment'
  params: {
    accountName: aiAccount.outputs.accountName
    projectName: aiProject.outputs.projectName
    cosmosDBConnection: aiProject.outputs.cosmosDBConnection 
    azureStorageConnection: aiProject.outputs.azureStorageConnection
    aiSearchConnection: aiProject.outputs.aiSearchConnection
    projectCapHost: projectCapHost
  }
  dependsOn: [
    aiSearchRoleAssignments, cosmosAccountRoleAssignments, storageAccountRoleAssignment
  ]
}




// The Cosmos DB Operator role must be assigned before the caphost is created
module cosmosContainerRoleAssignments 'modules-network-secured/cosmos-container-role-assignments.bicep' = {
    name: 'cosmos-role-assignments-${uniqueSuffix}-deployment'
    scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
    params: {
      cosmosAccountName: aiDependencies.outputs.cosmosDBName
      projectWorkspaceId: aiProject.outputs.projectWorkspaceId
      projectPrincipalId: aiProject.outputs.projectPrincipalId
    
    }
  dependsOn: [
    addProjectCapabilityHost
    ]
  }

  // The Storage Blob Data Owner role must be assigned before the caphost is created
module storageContainersRoleAssignment 'modules-network-secured/blob-storage-container-role-assignments.bicep' = {
    name: 'storage-containers-${uniqueSuffix}-deployment'
    scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
    params: { 
      aiProjectPrincipalId: aiProject.outputs.projectPrincipalId
      storageName: aiDependencies.outputs.azureStorageName
      workspaceId: aiProject.outputs.projectWorkspaceId
    }
    dependsOn: [
      addProjectCapabilityHost
    ]
  }
  
