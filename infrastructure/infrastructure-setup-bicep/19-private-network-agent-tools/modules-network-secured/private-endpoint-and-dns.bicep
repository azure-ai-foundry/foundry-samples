/*
Private Endpoint and DNS Configuration Module
------------------------------------------
This module configures private network access for Azure services using:

1. Private Endpoints:
   - Creates network interfaces in the specified subnet
   - Establishes private connections to Azure services
   - Enables secure access without public internet exposure

2. Private DNS Zones:
   - Enables custom DNS resolution for private endpoints

3. DNS Zone Links:
   - Links private DNS zones to the VNet
   - Enables name resolution for resources in the VNet
   - Prevents DNS resolution conflicts

Security Benefits:
- Eliminates public internet exposure
- Enables secure access from within VNet
- Prevents data exfiltration through network
*/

// Resource names and identifiers
@description('Name of the AI Foundry account')
param aiAccountName string
@description('Name of the AI Search service')
param aiSearchName string
@description('Name of the storage account')
param storageName string
@description('Name of the Cosmos DB account')
param cosmosDBName string
@description('The Microsoft Fabric Workspace full ARM Resource ID. Optional - leave empty to skip Fabric private endpoint.')
param fabricWorkspaceResourceId string = ''
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

@description('Subscription ID for AI Search service')
param aiSearchSubscriptionId string = subscription().subscriptionId

@description('Resource Group name for AI Search service')
param aiSearchResourceGroupName string = resourceGroup().name

@description('Subscription ID for Cosmos DB account')
param cosmosDBSubscriptionId string = subscription().subscriptionId

@description('Resource group name for Cosmos DB account')
param cosmosDBResourceGroupName string = resourceGroup().name

@description('Map of DNS zone FQDNs to an object describing where the zone lives. Each value must be an object with optional `subscriptionId` and `resourceGroup` properties. Empty `resourceGroup` means "create the zone in this deployment\'s resource group". Non-empty `resourceGroup` references an existing zone; empty `subscriptionId` defaults to the current subscription.')
param existingDnsZones object = {}

var requiredDnsZones = {
  'privatelink.services.ai.azure.com': { subscriptionId: '', resourceGroup: '' }
  'privatelink.openai.azure.com': { subscriptionId: '', resourceGroup: '' }
  'privatelink.cognitiveservices.azure.com': { subscriptionId: '', resourceGroup: '' }
  'privatelink.search.windows.net': { subscriptionId: '', resourceGroup: '' }
  'privatelink.blob.${environment().suffixes.storage}': { subscriptionId: '', resourceGroup: '' }
  'privatelink.documents.azure.com': { subscriptionId: '', resourceGroup: '' }
  'privatelink.fabric.microsoft.com': { subscriptionId: '', resourceGroup: '' }
}
var effectiveDnsZones = union(requiredDnsZones, existingDnsZones)

// ---- Resource references ----
resource aiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: aiAccountName
  scope: resourceGroup()
}

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' existing = {
  name: aiSearchName
  scope: resourceGroup(aiSearchSubscriptionId, aiSearchResourceGroupName)
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageName
  scope: resourceGroup(storageAccountSubscriptionId, storageAccountResourceGroupName)
}

resource cosmosDBAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' existing = {
  name: cosmosDBName
  scope: resourceGroup(cosmosDBSubscriptionId, cosmosDBResourceGroupName)
}

// ---- Fabric resource reference (conditional) ----
var fabricPassedIn = fabricWorkspaceResourceId != ''
var fabricParts = split(fabricWorkspaceResourceId, '/')
var fabricWorkspaceName = fabricPassedIn ? last(fabricParts) : ''

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

// Private endpoint for AI Services account
// - Creates network interface in customer hub subnet
// - Establishes private connection to AI Services account
resource aiAccountPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${aiAccountName}-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id } // Deploy in customer hub subnet
    privateLinkServiceConnections: [
      {
        name: '${aiAccountName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: aiAccount.id
          groupIds: ['account'] // Target AI Services account
        }
      }
    ]
  }
}

/* -------------------------------------------- AI Search Private Endpoint -------------------------------------------- */

// Private endpoint for AI Search
// - Creates network interface in customer hub subnet
// - Establishes private connection to AI Search service
resource aiSearchPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${aiSearchName}-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id } // Deploy in customer hub subnet
    privateLinkServiceConnections: [
      {
        name: '${aiSearchName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: aiSearch.id
          groupIds: ['searchService'] // Target search service
        }
      }
    ]
  }
}

/* -------------------------------------------- Storage Private Endpoint -------------------------------------------- */

// Private endpoint for Storage Account
// - Creates network interface in customer hub subnet
// - Establishes private connection to blob storage
resource storagePrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${storageName}-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id } // Deploy in customer hub subnet
    privateLinkServiceConnections: [
      {
        name: '${storageName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: storageAccount.id // Target blob storage
          groupIds: ['blob']
        }
      }
    ]
  }
}

/*--------------------------------------------- Cosmos DB Private Endpoint -------------------------------------*/

resource cosmosDBPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${cosmosDBName}-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id } // Deploy in customer hub subnet
    privateLinkServiceConnections: [
      {
        name: '${cosmosDBName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: cosmosDBAccount.id // Target Cosmos DB account
          groupIds: ['Sql']
        }
      }
    ]
  }
}

/*--------------------------------------------- Microsoft Fabric Private Endpoint -------------------------------------*/

// Private endpoint for Microsoft Fabric Workspace
// - Creates network interface in customer private endpoint subnet
// - Establishes private connection to Fabric workspace
// - Only created if fabricWorkspaceResourceId is provided
resource fabricPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = if (fabricPassedIn) {
  name: '${fabricWorkspaceName}-fabric-private-endpoint'
  location: resourceGroup().location
  properties: {
    subnet: { id: peSubnet.id } // Deploy in customer private endpoint subnet
    privateLinkServiceConnections: [
      {
        name: '${fabricWorkspaceName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: fabricWorkspaceResourceId // Target Fabric workspace
          groupIds: ['Fabric'] // Fabric private link group
        }
      }
    ]
  }
}

/* -------------------------------------------- Private DNS Zones --------------------------------------------

   This block used to declare 7 zones × 3 resources each (~150 lines). It is now a single
   `for` loop over `existingDnsZones`, with one tiny sub-module (`private-dns-zone.bicep`)
   per zone. Per-PE DNS zone groups below look up zone IDs by zone name via `indexOf`.

   To skip the optional Fabric zone when no Fabric workspace is provided, the corresponding
   module call is disabled (`enabled: false`) instead of being filtered out — keeping the
   array indices stable and `indexOf` lookups safe.
*/

var aiServicesDnsZoneName = 'privatelink.services.ai.azure.com'
var openAiDnsZoneName = 'privatelink.openai.azure.com'
var cognitiveServicesDnsZoneName = 'privatelink.cognitiveservices.azure.com'
var aiSearchDnsZoneName = 'privatelink.search.windows.net'
var storageDnsZoneName = 'privatelink.blob.${environment().suffixes.storage}'
var cosmosDBDnsZoneName = 'privatelink.documents.azure.com'
var fabricDnsZoneName = 'privatelink.fabric.microsoft.com'

var dnsZoneEntries = items(effectiveDnsZones)
var dnsZoneKeys = map(dnsZoneEntries, e => e.key)

module dnsZones 'private-dns-zone.bicep' = [for (entry, i) in dnsZoneEntries: {
  name: 'dns-${replace(entry.key, '.', '-')}-${suffix}'
  params: {
    zoneName: entry.key
    existingResourceGroup: entry.value.?resourceGroup ?? ''
    existingSubscriptionId: entry.value.?subscriptionId ?? ''
    vnetId: vnet.id
    suffix: suffix
    // Disable the Fabric zone when no Fabric workspace was supplied — saves a no-op zone.
    enabled: entry.key == fabricDnsZoneName ? fabricPassedIn : true
  }
}]

// ---- Per-zone ID lookups (used to wire DNS zone groups onto each Private Endpoint) ----
var aiServicesDnsZoneId       = dnsZones[indexOf(dnsZoneKeys, aiServicesDnsZoneName)].outputs.zoneId
var openAiDnsZoneId           = dnsZones[indexOf(dnsZoneKeys, openAiDnsZoneName)].outputs.zoneId
var cognitiveServicesDnsZoneId = dnsZones[indexOf(dnsZoneKeys, cognitiveServicesDnsZoneName)].outputs.zoneId
var aiSearchDnsZoneId         = dnsZones[indexOf(dnsZoneKeys, aiSearchDnsZoneName)].outputs.zoneId
var storageDnsZoneId          = dnsZones[indexOf(dnsZoneKeys, storageDnsZoneName)].outputs.zoneId
var cosmosDBDnsZoneId         = dnsZones[indexOf(dnsZoneKeys, cosmosDBDnsZoneName)].outputs.zoneId
var fabricDnsZoneId           = fabricPassedIn ? dnsZones[indexOf(dnsZoneKeys, fabricDnsZoneName)].outputs.zoneId : ''

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
  // Implicit dependencies on the dnsZones[*] modules via the *DnsZoneId vars above.
}
resource aiSearchDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: aiSearchPrivateEndpoint
  name: '${aiSearchName}-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      { name: '${aiSearchName}-dns-config', properties: { privateDnsZoneId: aiSearchDnsZoneId } }
    ]
  }
}
resource storageDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: storagePrivateEndpoint
  name: '${storageName}-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      { name: '${storageName}-dns-config', properties: { privateDnsZoneId: storageDnsZoneId } }
    ]
  }
}
resource cosmosDBDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: cosmosDBPrivateEndpoint
  name: '${cosmosDBName}-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      { name: '${cosmosDBName}-dns-config', properties: { privateDnsZoneId: cosmosDBDnsZoneId } }
    ]
  }
}

// Fabric DNS Zone Group - only created if Fabric workspace is provided
resource fabricDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = if (fabricPassedIn) {
  parent: fabricPrivateEndpoint
  name: '${fabricWorkspaceName}-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      { name: '${fabricWorkspaceName}-dns-config', properties: { privateDnsZoneId: fabricDnsZoneId } }
    ]
  }
}
