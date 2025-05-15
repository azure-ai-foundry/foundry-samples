param accountName string
param location string
param projectName string
param projectDescription string  
param displayName string

param aiSearchName string
param aiSearchServiceResourceGroupName string
param aiSearchServiceSubscriptionId string

param cosmosDBName string
param cosmosDBSubscriptionId string
param cosmosDBResourceGroupName string

param azureStorageName string 
param azureStorageSubscriptionId string
param azureStorageResourceGroupName string

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' existing = {
  name: aiSearchName
  scope: resourceGroup(aiSearchServiceSubscriptionId, aiSearchServiceResourceGroupName)
}
resource cosmosDBAccount 'Microsoft.DocumentDB/databaseAccounts@2024-12-01-preview' existing = {
  name: cosmosDBName
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
}
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: azureStorageName
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
}

#disable-next-line BCP081
resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
  scope: resourceGroup()
}

#disable-next-line BCP081
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: account
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    description: projectDescription
    displayName: displayName
  }

  #disable-next-line BCP081
  resource project_connection_cosmosdb_account 'connections@2025-04-01-preview' = {
    name: cosmosDBName
    properties: {
      category: 'CosmosDB'
      target: cosmosDBAccount.properties.documentEndpoint
      authType: 'AAD'
      metadata: {
        ApiType: 'Azure'
        ResourceId: cosmosDBAccount.id
        location: cosmosDBAccount.location
      }
    }
  }

  #disable-next-line BCP081
  resource project_connection_azure_storage 'connections@2025-04-01-preview' = {
    name: azureStorageName
    properties: {
      category: 'AzureStorageAccount'
      target: storageAccount.properties.primaryEndpoints.blob
      authType: 'AAD'
      metadata: {
        ApiType: 'Azure'
        ResourceId: storageAccount.id
        location: storageAccount.location
      }
    }
  }

  #disable-next-line BCP081
  resource project_connection_azureai_search 'connections@2025-04-01-preview' = {
    name: aiSearchName
    properties: {
      category: 'CognitiveSearch'
      target: 'https://${aiSearchName}.search.windows.net'
      authType: 'AAD'
      metadata: {
        ApiType: 'Azure'
        ResourceId: searchService.id
        location: searchService.location
      }
    }
  }

}
// This workaround is no longer needed
// // Assign Project SMI - Azure AI Developer Role
// // Most likely not permanent, but for now, this is the only way to assign the role to the project SMI
// resource azureAIDeveloperRoleId 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
//   name: '64702f94-c441-49e6-a78b-ef80e0188fee'  // Built-in role ID
//   scope: resourceGroup()
// }


// resource projectSMIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
//   name: guid(azureAIDeveloperRoleId.id, accountName, account.id, projectName)
//   scope: project
//   properties: {
//     principalId: project.identity.principalId
//     principalType: 'ServicePrincipal'
//     roleDefinitionId: azureAIDeveloperRoleId.id
//   }
// }

output projectName string = project.name
output projectId string = project.id
output projectPrincipalId string = project.identity.principalId
output projectWorkspaceId string = project.properties.amlWorkspace.internalId

// return the BYO connection names
output cosmosDBConnection string = cosmosDBName
output azureStorageConnection string = azureStorageName
output aiSearchConnection string = aiSearchName
