param accountName string
param projectName string
param cosmosDBName string
param azureStorageName string 
param aiSearchName string


resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
  scope: resourceGroup()
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' existing = {
  parent: account
  name: projectName

  resource project_connection_cosmosdb_account 'connections@2025-04-01-preview' existing = {
    name: cosmosDBName
  }

  resource project_connection_azure_storage 'connections@2025-04-01-preview' existing= {
    name: azureStorageName
  }

  resource project_connection_ai_search 'connections@2025-04-01-preview' existing= {
    name: aiSearchName
  }
}

output projectName string = project.name
output projectId string = project.id
output projectPrincipalId string = project.identity.principalId
output projectWorkspaceId string = project.properties.amlWorkspace.internalId

// return the BYO connection names
output cosmosDBConnection string = cosmosDBName
output azureStorageConnection string = azureStorageName
output aiSearchConnection string = aiSearchName

