
param projectCapHost string = 'caphostproj'
// Create a short, unique suffix, that will be unique to each resource group
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)

module mainCreate 'main-create.bicep' = {
  name: 'main-create-deployment'
  scope: resourceGroup()
}

// The Cosmos DB Operator role must be assigned before the caphost is created
module cosmosContainerRoleAssignments 'modules-network-secured/cosmos-container-role-assignments.bicep' = {
  name: 'cosmos-role-assignments-${uniqueSuffix}-deployment' 
  scope: resourceGroup()
  params: {
    cosmosAccountName: mainCreate.outputs.cosmosDBName
    projectWorkspaceId: mainCreate.outputs.projectWorkspaceId
    projectPrincipalId: mainCreate.outputs.projectPrincipalId
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
    aiProjectPrincipalId: mainCreate.outputs.projectPrincipalId
    storageName: mainCreate.outputs.azureStorageName
    workspaceId: mainCreate.outputs.projectWorkspaceId
  }
  dependsOn: [
    addProjectCapabilityHost
  ]
}

// This module creates the capability host for the project and account
module addProjectCapabilityHost 'modules-network-secured/add-project-capability-host.bicep' = {
  name: 'capabilityHost-configuration-${uniqueSuffix}-deployment'
  params: {
    accountName: mainCreate.outputs.accountName
    projectName: mainCreate.outputs.projectName
    cosmosDBConnection: mainCreate.outputs.cosmosDBConnection 
    azureStorageConnection: mainCreate.outputs.azureStorageConnection
    aiSearchConnection: mainCreate.outputs.aiSearchConnection
    projectCapHost: projectCapHost
  }
}



