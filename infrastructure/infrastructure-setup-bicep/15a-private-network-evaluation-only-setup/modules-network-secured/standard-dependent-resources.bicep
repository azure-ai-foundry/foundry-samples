// Creates Azure dependent resources for Azure AI evaluation-only setup
// Only creates Storage Account (no Cosmos DB or AI Search)

@description('Azure region of the deployment')
param location string

@description('Name of the storage account')
param azureStorageName string

@description('The AI Storage Account full ARM Resource ID. This is an optional field, and if not provided, the resource will be created.')
param azureStorageAccountResourceId string

param azureStorageExists bool

var azureStorageParts = split(azureStorageAccountResourceId, '/')

resource existingAzureStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = if (azureStorageExists) {
  name: azureStorageParts[8]
  scope: resourceGroup(azureStorageParts[2], azureStorageParts[4])
}

// Some regions doesn't support Standard Zone-Redundant storage, need to use Geo-redundant storage
param noZRSRegions array = ['southindia', 'westus']
param sku object = contains(noZRSRegions, location) ? { name: 'Standard_GRS' } : { name: 'Standard_ZRS' }

// Storage creation
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = if(!azureStorageExists) {
  name: azureStorageName
  location: location
  kind: 'StorageV2'
  sku: sku
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
      virtualNetworkRules: []
    }
    allowSharedKeyAccess: false
  }
}

output azureStorageName string = azureStorageExists ? existingAzureStorageAccount.name : storage.name
output azureStorageId string = azureStorageExists ? existingAzureStorageAccount.id : storage.id
output azureStorageResourceGroupName string = azureStorageExists ? azureStorageParts[4] : resourceGroup().name
output azureStorageSubscriptionId string = azureStorageExists ? azureStorageParts[2] : subscription().subscriptionId
