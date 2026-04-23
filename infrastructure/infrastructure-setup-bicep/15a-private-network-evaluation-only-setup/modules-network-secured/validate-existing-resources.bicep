// Validate existing resources for evaluation-only setup (Storage Account only)

@description('Resource ID of the Azure Storage Account.')
param azureStorageAccountResourceId string

// Check if existing resources have been passed in
var storagePassedIn = azureStorageAccountResourceId != ''

var storageParts = split(azureStorageAccountResourceId, '/')
var azureStorageSubscriptionId = storagePassedIn && length(storageParts) > 2 ? storageParts[2] : subscription().subscriptionId
var azureStorageResourceGroupName = storagePassedIn && length(storageParts) > 4 ? storageParts[4] : resourceGroup().name

// Validate Storage Account
resource azureStorageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = if (storagePassedIn) {
  name: last(split(azureStorageAccountResourceId, '/'))
  scope: resourceGroup(azureStorageSubscriptionId, azureStorageResourceGroupName)
}

output azureStorageExists bool = storagePassedIn && (azureStorageAccount.name == storageParts[8])

output azureStorageSubscriptionId string = azureStorageSubscriptionId
output azureStorageResourceGroupName string = azureStorageResourceGroupName

// Adding DNS Zone Check

@description('Object mapping DNS zone names to their resource group, or empty string to indicate creation')
param existingDnsZones object

@description('Subscription ID where existing private DNS zones are located. Should be resolved to current subscription if empty.')
param dnsZonesSubscriptionId string

@description('List of private DNS zone names to validate')
param dnsZoneNames array

// Output whether each DNS zone exists
output dnsZoneExists array = [
  for zoneName in dnsZoneNames: {
    name: zoneName
    exists: !empty(existingDnsZones[zoneName])
  }
]
