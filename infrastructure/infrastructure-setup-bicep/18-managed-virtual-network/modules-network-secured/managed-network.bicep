@description('The name of the AI Services account')
param accountName string

@description('The isolation mode for the managed network')
@allowed([
  'AllowOnlyApprovedOutbound'
  'AllowInternetOutbound'
])
param isolationMode string = 'AllowOnlyApprovedOutbound'

@description('Resource ID of the Storage Account for outbound PE rule')
param storageAccountResourceId string

@description('Resource ID of the Cosmos DB Account for outbound PE rule')
param cosmosDBResourceId string

@description('Resource ID of the AI Search Service for outbound PE rule')
param aiSearchResourceId string

// Reference the existing AI Services account in the same resource group
resource aiAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

// Create the managed network settings first
#disable-next-line BCP081
resource managedNetwork 'Microsoft.CognitiveServices/accounts/managednetworks@2025-10-01-preview' = {
  parent: aiAccount
  name: 'default'
  properties: {
    managedNetwork: {
      IsolationMode: isolationMode
      managedNetworkKind: 'V2'
      provisionNetworkNow: true
      //firewallSku: 'Standard' // Uncomment to enable firewall only when in AllowOnlyApprovedOutbound mode
    }
  }
}

// Outbound PE rules allow the managed VNet (where hosted agents run) to reach dependent resources
// Rules must be created sequentially to avoid conflicting state errors on the managed network
#disable-next-line BCP081
resource storageOutboundRule 'Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview' = {
  parent: managedNetwork
  name: 'storage-blob-rule'
  properties: {
    type: 'PrivateEndpoint'
    destination: {
      serviceResourceId: storageAccountResourceId
      subresourceTarget: 'blob'
    }
    category: 'UserDefined'
  }
}

#disable-next-line BCP081
resource cosmosDBOutboundRule 'Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview' = {
  parent: managedNetwork
  name: 'cosmos-sql-rule'
  properties: {
    type: 'PrivateEndpoint'
    destination: {
      serviceResourceId: cosmosDBResourceId
      subresourceTarget: 'Sql'
    }
    category: 'UserDefined'
  }
  dependsOn: [storageOutboundRule]
}

#disable-next-line BCP081
resource aiSearchOutboundRule 'Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview' = {
  parent: managedNetwork
  name: 'aisearch-rule'
  properties: {
    type: 'PrivateEndpoint'
    destination: {
      serviceResourceId: aiSearchResourceId
      subresourceTarget: 'searchService'
    }
    category: 'UserDefined'
  }
  dependsOn: [cosmosDBOutboundRule]
}

output managedNetworkSettingsName string = managedNetwork.name
