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

@description('Resource ID of the Azure Monitor Private Link Scope for telemetry')
param amplsResourceId string

@description('Resource ID of the Azure Container Registry for outbound PE rule. When empty, no ACR outbound rule is created.')
param acrResourceId string = ''

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

// The agent needs a PE back to the Foundry/AI Services endpoint itself (public access is disabled)
#disable-next-line BCP081
resource aiServicesOutboundRule 'Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview' = {
  parent: managedNetwork
  name: 'aiservices-account-rule'
  properties: {
    type: 'PrivateEndpoint'
    destination: {
      serviceResourceId: aiAccount.id
      subresourceTarget: 'account'
    }
    category: 'UserDefined'
  }
}

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
  dependsOn: [aiServicesOutboundRule]
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

// Outbound PE rule for Azure Monitor Private Link Scope (AMPLS)
// This allows the hosted agent to export telemetry to Application Insights privately
#disable-next-line BCP081
resource amplsOutboundRule 'Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview' = {
  parent: managedNetwork
  name: 'ampls-monitor-rule'
  properties: {
    type: 'PrivateEndpoint'
    destination: {
      serviceResourceId: amplsResourceId
      subresourceTarget: 'azuremonitor'
    }
    category: 'UserDefined'
  }
  dependsOn: [aiSearchOutboundRule]
}

// Outbound PE rule for Azure Container Registry
// This allows the hosted agent to pull container images from the private ACR
#disable-next-line BCP081
resource acrOutboundRule 'Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview' = if (!empty(acrResourceId)) {
  parent: managedNetwork
  name: 'acr-registry-rule'
  properties: {
    type: 'PrivateEndpoint'
    destination: {
      serviceResourceId: acrResourceId
      subresourceTarget: 'registry'
    }
    category: 'UserDefined'
  }
  dependsOn: [amplsOutboundRule]
}

output managedNetworkSettingsName string = managedNetwork.name
