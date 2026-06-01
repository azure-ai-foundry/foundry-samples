// Adds the three Standard Agent connections (Cosmos DB, Storage, AI Search) onto
// an EXISTING AI Foundry project, in place. This mirrors
// ai-project-identity-unique.bicep but references the project with the `existing`
// keyword so the project itself is reused (no new project, no random suffix).
// Outputs are identical to ai-project-identity-unique.bicep so every downstream
// module (role assignments, capability host, container roles) stays unchanged.

param accountName string
param projectName string

param aiSearchName string
param aiSearchServiceResourceGroupName string
param aiSearchServiceSubscriptionId string

param cosmosDBName string
param cosmosDBSubscriptionId string
param cosmosDBResourceGroupName string

param azureStorageName string
param azureStorageSubscriptionId string
param azureStorageResourceGroupName string

@description('Full name of the Cosmos DB connection to create or update on the project. The capability host binds to this exact name.')
param cosmosDBConnectionName string

@description('Full name of the Storage connection to create or update on the project. The capability host binds to this exact name.')
param azureStorageConnectionName string

@description('Full name of the AI Search connection to create or update on the project. The capability host binds to this exact name.')
param aiSearchConnectionName string

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

resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
  scope: resourceGroup()
}

// Reference the project that already exists (do NOT create it).
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' existing = {
  parent: account
  name: projectName

  // Child connections are a create/update on the existing project, with the same
  // names the capability host binds to.
  resource project_connection_cosmosdb_account 'connections@2025-04-01-preview' = {
    name: cosmosDBConnectionName
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

  resource project_connection_azure_storage 'connections@2025-04-01-preview' = {
    name: azureStorageConnectionName
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

  resource project_connection_azureai_search 'connections@2025-04-01-preview' = {
    name: aiSearchConnectionName
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

output projectName string = project.name
output projectId string = project.id
output projectPrincipalId string = project.identity.principalId

#disable-next-line BCP053
output projectWorkspaceId string = project.properties.internalId

// Return the connection names so the capability host binds to the exact strings.
output cosmosDBConnection string = cosmosDBConnectionName
output azureStorageConnection string = azureStorageConnectionName
output aiSearchConnection string = aiSearchConnectionName
