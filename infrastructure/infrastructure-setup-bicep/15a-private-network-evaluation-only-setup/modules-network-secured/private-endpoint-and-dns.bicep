/*
Private Endpoint and DNS Configuration Module (Evaluation-Only)
------------------------------------------
This module configures private network access for AI Services and Storage only.
No Cosmos DB or AI Search private endpoints are created.

1. Private Endpoints:
   - AI Foundry Account
   - Azure Storage (blob)

2. Private DNS Zones:
   - privatelink.services.ai.azure.com
   - privatelink.openai.azure.com
   - privatelink.cognitiveservices.azure.com
   - privatelink.blob.core.windows.net

3. DNS Zone Links and DNS Zone Groups
*/

// Resource names and identifiers
@description('Name of the AI Foundry account')
param aiAccountName string
@description('Name of the storage account')
param storageName string
@description('Name of the Vnet')
param vnetName string
@description('Name of the Customer subnet')
param peSubnetName string
@description('Suffix for unique resource names')
param suffix string

@description('Resource Group name for existing Virtual Network (if different from current resource group)')
param vnetResourceGroupName string = resourceGroup().name

@description('Subscription ID for Virtual Network')
param vnetSubscriptionId string = subscription().subscriptionId

@description('Resource Group name for Storage Account')
param storageAccountResourceGroupName string = resourceGroup().name

@description('Subscription ID for Storage account')
param storageAccountSubscriptionId string = subscription().subscriptionId

@description('Map of DNS zone FQDNs to resource group names. If provided, reference existing DNS zones in this resource group instead of creating them.')
param existingDnsZones object = {
  'privatelink.services.ai.azure.com': ''
  'privatelink.openai.azure.com': ''
  'privatelink.cognitiveservices.azure.com': ''
  'privatelink.blob.${environment().suffixes.storage}': ''
}

@description('Subscription ID where existing private DNS zones are located. Should be resolved to current subscription if empty.')
param dnsZonesSubscriptionId string

// ---- Resource references ----
resource aiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: aiAccountName
  scope: resourceGroup()
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageName
  scope: resourceGroup(storageAccountSubscriptionId, storageAccountResourceGroupName)
}

// Reference existing network resources
resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: vnetName
  scope: resourceGroup(vnetSubscriptionId, vnetResourceGroupName)
}
resource peSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: vnet
  name: peSubnetName
}

/* -------------------------------------------- AI Foundry Account Private Endpoint -------------------------------------------- */

resource aiAccountPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${aiAccountName}-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id }
    privateLinkServiceConnections: [
      {
        name: '${aiAccountName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: aiAccount.id
          groupIds: [ 'account' ]
        }
      }
    ]
  }
}

/* -------------------------------------------- Storage Private Endpoint -------------------------------------------- */

resource storagePrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${storageName}-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id }
    privateLinkServiceConnections: [
      {
        name: '${storageName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [ 'blob' ]
        }
      }
    ]
  }
}

/* -------------------------------------------- Private DNS Zones -------------------------------------------- */

var aiServicesDnsZoneName = 'privatelink.services.ai.azure.com'
var openAiDnsZoneName = 'privatelink.openai.azure.com'
var cognitiveServicesDnsZoneName = 'privatelink.cognitiveservices.azure.com'
var storageDnsZoneName = 'privatelink.blob.${environment().suffixes.storage}'

// ---- DNS Zone Resource Group lookups ----
var aiServicesDnsZoneRG = existingDnsZones[aiServicesDnsZoneName]
var openAiDnsZoneRG = existingDnsZones[openAiDnsZoneName]
var cognitiveServicesDnsZoneRG = existingDnsZones[cognitiveServicesDnsZoneName]
var storageDnsZoneRG = existingDnsZones[storageDnsZoneName]

// ---- DNS Zone Resources and References ----
resource aiServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(aiServicesDnsZoneRG)) {
  name: aiServicesDnsZoneName
  location: 'global'
}

resource existingAiServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(aiServicesDnsZoneRG)) {
  name: aiServicesDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, aiServicesDnsZoneRG)
}
var aiServicesDnsZoneId = empty(aiServicesDnsZoneRG) ? aiServicesPrivateDnsZone.id : existingAiServicesPrivateDnsZone.id

resource openAiPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(openAiDnsZoneRG)) {
  name: openAiDnsZoneName
  location: 'global'
}

resource existingOpenAiPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(openAiDnsZoneRG)) {
  name: openAiDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, openAiDnsZoneRG)
}
var openAiDnsZoneId = empty(openAiDnsZoneRG) ? openAiPrivateDnsZone.id : existingOpenAiPrivateDnsZone.id

resource cognitiveServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(cognitiveServicesDnsZoneRG)) {
  name: cognitiveServicesDnsZoneName
  location: 'global'
}

resource existingCognitiveServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(cognitiveServicesDnsZoneRG)) {
  name: cognitiveServicesDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, cognitiveServicesDnsZoneRG)
}
var cognitiveServicesDnsZoneId = empty(cognitiveServicesDnsZoneRG) ? cognitiveServicesPrivateDnsZone.id : existingCognitiveServicesPrivateDnsZone.id

resource storagePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(storageDnsZoneRG)) {
  name: storageDnsZoneName
  location: 'global'
}

resource existingStoragePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(storageDnsZoneRG)) {
  name: storageDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, storageDnsZoneRG)
}
var storageDnsZoneId = empty(storageDnsZoneRG) ? storagePrivateDnsZone.id : existingStoragePrivateDnsZone.id

// ---- DNS VNet Links ----
resource aiServicesLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (empty(aiServicesDnsZoneRG)) {
  parent: aiServicesPrivateDnsZone
  location: 'global'
  name: 'aiServices-${suffix}-link'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}
resource openAiLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (empty(openAiDnsZoneRG)) {
  parent: openAiPrivateDnsZone
  location: 'global'
  name: 'aiServicesOpenAI-${suffix}-link'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}
resource cognitiveServicesLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (empty(cognitiveServicesDnsZoneRG)) {
  parent: cognitiveServicesPrivateDnsZone
  location: 'global'
  name: 'aiServicesCognitiveServices-${suffix}-link'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}
resource storageLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (empty(storageDnsZoneRG)) {
  parent: storagePrivateDnsZone
  location: 'global'
  name: 'storage-${suffix}-link'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}

// ---- DNS Zone Groups ----
resource aiServicesDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: aiAccountPrivateEndpoint
  name: '${aiAccountName}-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      { name: '${aiAccountName}-dns-aiserv-config', properties: { privateDnsZoneId: aiServicesDnsZoneId } }
      { name: '${aiAccountName}-dns-openai-config', properties: { privateDnsZoneId: openAiDnsZoneId } }
      { name: '${aiAccountName}-dns-cogserv-config', properties: { privateDnsZoneId: cognitiveServicesDnsZoneId } }
    ]
  }
  dependsOn: [
    empty(aiServicesDnsZoneRG) ? aiServicesLink : null
    empty(openAiDnsZoneRG) ? openAiLink : null
    empty(cognitiveServicesDnsZoneRG) ? cognitiveServicesLink : null
  ]
}
resource storageDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: storagePrivateEndpoint
  name: '${storageName}-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      { name: '${storageName}-dns-config', properties: { privateDnsZoneId: storageDnsZoneId } }
    ]
  }
  dependsOn: [
    empty(storageDnsZoneRG) ? storageLink : null
  ]
}
