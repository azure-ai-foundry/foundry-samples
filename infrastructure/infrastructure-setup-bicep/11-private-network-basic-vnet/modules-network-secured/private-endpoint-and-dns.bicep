/*
Private Endpoint and DNS Configuration Module
------------------------------------------
This module configures private network access for the AI Services account using:

1. Private Endpoints:
   - Creates network interface in the specified subnet
   - Establishes private connection to AI Services account
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
@description('Name of the Vnet')
param vnetName string
@description('Name of the private endpoint subnet')
param peSubnetName string
@description('Suffix for unique resource names')
param suffix string

@description('Resource Group name for existing Virtual Network (if different from current resource group)')
param vnetResourceGroupName string = resourceGroup().name

@description('Subscription ID for Virtual Network')
param vnetSubscriptionId string = subscription().subscriptionId

@description('Map of DNS zone FQDNs to resource group names. If provided, reference existing DNS zones in this resource group instead of creating them.')
param existingDnsZones object = {
  'privatelink.services.ai.azure.com': ''
  'privatelink.openai.azure.com': ''
  'privatelink.cognitiveservices.azure.com': ''
}

@description('Subscription ID where existing private DNS zones are located. Should be resolved to current subscription if empty.')
param dnsZonesSubscriptionId string

// ---- Resource references ----
resource aiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: aiAccountName
  scope: resourceGroup()
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
          groupIds: [ 'account' ] // Target AI Services account
        }
      }
    ]
  }
}

/* -------------------------------------------- Private DNS Zones -------------------------------------------- */

// Format: 1) Private DNS Zone
//         2) Link Private DNS Zone to VNet
//         3) Create DNS Zone Group for Private Endpoint

var aiServicesDnsZoneName = 'privatelink.services.ai.azure.com'
var openAiDnsZoneName = 'privatelink.openai.azure.com'
var cognitiveServicesDnsZoneName = 'privatelink.cognitiveservices.azure.com'

// ---- DNS Zone Resource Group lookups ----
var aiServicesDnsZoneRG = existingDnsZones[aiServicesDnsZoneName]
var openAiDnsZoneRG = existingDnsZones[openAiDnsZoneName]
var cognitiveServicesDnsZoneRG = existingDnsZones[cognitiveServicesDnsZoneName]

// ---- DNS Zone Resources and References ----
resource aiServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(aiServicesDnsZoneRG)) {
  name: aiServicesDnsZoneName
  location: 'global'
}

// Reference existing private DNS zone if provided
resource existingAiServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(aiServicesDnsZoneRG)) {
  name: aiServicesDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, aiServicesDnsZoneRG)
}
//creating condition if user pass existing dns zones or not
var aiServicesDnsZoneId = empty(aiServicesDnsZoneRG) ? aiServicesPrivateDnsZone.id : existingAiServicesPrivateDnsZone.id

resource openAiPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(openAiDnsZoneRG)) {
  name: openAiDnsZoneName
  location: 'global'
}

// Reference existing private DNS zone if provided
resource existingOpenAiPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(openAiDnsZoneRG)) {
  name: openAiDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, openAiDnsZoneRG)
}
//creating condition if user pass existing dns zones or not
var openAiDnsZoneId = empty(openAiDnsZoneRG) ? openAiPrivateDnsZone.id : existingOpenAiPrivateDnsZone.id

resource cognitiveServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (empty(cognitiveServicesDnsZoneRG)) {
  name: cognitiveServicesDnsZoneName
  location: 'global'
}

// Reference existing private DNS zone if provided
resource existingCognitiveServicesPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (!empty(cognitiveServicesDnsZoneRG)) {
  name: cognitiveServicesDnsZoneName
  scope: resourceGroup(dnsZonesSubscriptionId, cognitiveServicesDnsZoneRG)
}
//creating condition if user pass existing dns zones or not
var cognitiveServicesDnsZoneId = empty(cognitiveServicesDnsZoneRG) ? cognitiveServicesPrivateDnsZone.id : existingCognitiveServicesPrivateDnsZone.id

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
