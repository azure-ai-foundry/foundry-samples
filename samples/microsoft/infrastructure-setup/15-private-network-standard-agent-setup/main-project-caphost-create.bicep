param projectCapHost string = 'caphostproj'
// Create a short, unique suffix, that will be unique to each resource group
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)

param accountName string
param projectName string
param aiSearchName string
param azureStorageName string
param cosmosDBName string

// The name of the account to reference
module accountCreate 'modules-network-secured/ai-account-reference.bicep' = {
  name: 'account-${uniqueSuffix}-deployment'
  scope: resourceGroup()
  params: {
    accountName: accountName
  }
}

// The name of standard created resource to reference
module standardCreate 'modules-network-secured/standard-dependent-reference.bicep' = {
  name: 'standard-${uniqueSuffix}-deployment'
  scope: resourceGroup()
  params: {
    aiSearchName: aiSearchName
    azureStorageName: azureStorageName
    cosmosDBName: cosmosDBName
  }
}

// The name of the project to reference
module projectCreate 'modules-network-secured/ai-project-reference.bicep' = {
  name: 'project-${uniqueSuffix}-deployment'
  scope: resourceGroup()
  params: {
    accountName: accountCreate.outputs.accountName
    projectName: projectName
    aiSearchName: standardCreate.outputs.aiSearchName
    azureStorageName: standardCreate.outputs.azureStorageName
    cosmosDBName: standardCreate.outputs.cosmosDBName
  }
}

// This module creates the capability host for the project and account
module addProjectCapabilityHost 'modules-network-secured/add-project-capability-host.bicep' = {
  name: 'capabilityHost-configuration-${uniqueSuffix}-deployment'
  params: {
    accountName: accountCreate.outputs.accountName
    projectName: projectCreate.outputs.projectName
    cosmosDBConnection: projectCreate.outputs.cosmosDBConnection 
    azureStorageConnection: projectCreate.outputs.azureStorageConnection
    aiSearchConnection: projectCreate.outputs.aiSearchConnection
    projectCapHost: projectCapHost
  }
}

// The Cosmos DB Operator role must be assigned before the caphost is created
module cosmosContainerRoleAssignments 'modules-network-secured/cosmos-container-role-assignments.bicep' = {
  name: 'cosmos-role-assignments-${uniqueSuffix}-deployment' 
  scope: resourceGroup()
  params: {
    cosmosAccountName: standardCreate.outputs.cosmosDBName
    projectWorkspaceId: projectCreate.outputs.projectWorkspaceId
    projectPrincipalId: projectCreate.outputs.projectPrincipalId
  }
dependsOn: [
  addProjectCapabilityHost
  ]
}


// The Storage Blob Data Owner role must be assigned before the caphost is created
module storageContainersRoleAssignment 'modules-network-secured/blob-storage-container-role-assignments.bicep' = {
  name: 'storage-containers-${uniqueSuffix}-deployment'
  scope: resourceGroup()
  params: { 
    aiProjectPrincipalId: projectCreate.outputs.projectPrincipalId
    storageName: standardCreate.outputs.azureStorageName
    workspaceId: projectCreate.outputs.projectWorkspaceId
  }
  dependsOn: [
    addProjectCapabilityHost
  ]
}



