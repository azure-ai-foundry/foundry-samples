@description('The name of the AI Search resource to reference')
param aiSearchName string

@description('Name of the storage account to reference')
param azureStorageName string

@description('Name of the new Cosmos DB account to reference')
param cosmosDBName string


resource cosmosDB 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosDBName
}

resource aiSearch 'Microsoft.Search/searchServices@2024-06-01-preview' existing =  {
  name: aiSearchName
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: azureStorageName

}

output aiSearchName string = aiSearch.name
output azureStorageName string = storage.name  
output cosmosDBName string = cosmosDB.name
