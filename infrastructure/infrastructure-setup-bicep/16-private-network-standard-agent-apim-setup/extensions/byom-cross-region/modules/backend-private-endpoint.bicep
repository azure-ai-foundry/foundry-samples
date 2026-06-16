/*
  backend-private-endpoint.bicep
  ------------------------------
  Cross-region private endpoint into the backend Foundry account.

  The PE lives on the backend-pe subnet in the PROJECT region VNet, so
  the project Foundry account, APIM, and the agent compute can all reach
  the backend account over the Microsoft backbone without any of the
  traffic leaving the customer VNet.

  Wired into the existing privatelink.cognitiveservices.azure.com,
  privatelink.openai.azure.com, and privatelink.services.ai.azure.com
  private DNS zones that template 16 already links to the VNet.
*/

@description('Project-region location for the PE NIC (NOT the backend account region).')
param location string

@description('Backend Foundry account resource ID — the cross-region target.')
param backendAccountResourceId string

@description('Resource ID of the backend-pe subnet (in the project region VNet).')
param backendPeSubnetId string

@description('Suffix used to name the PE + DNS zone group.')
param suffix string

@description('Existing private DNS zone for privatelink.cognitiveservices.azure.com (linked to the VNet by template 16).')
param cognitiveServicesDnsZoneId string

@description('Existing private DNS zone for privatelink.openai.azure.com (linked to the VNet by template 16).')
param openAiDnsZoneId string

@description('Existing private DNS zone for privatelink.services.ai.azure.com (linked to the VNet by template 16).')
param servicesAiDnsZoneId string

var peName = 'backend-account-pe-${suffix}'

resource backendPe 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: peName
  location: location
  properties: {
    subnet: {
      id: backendPeSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: peName
        properties: {
          privateLinkServiceId: backendAccountResourceId
          groupIds: [
            'account'
          ]
        }
      }
    ]
  }
}

resource backendPeDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: backendPe
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-cognitiveservices'
        properties: {
          privateDnsZoneId: cognitiveServicesDnsZoneId
        }
      }
      {
        name: 'privatelink-openai'
        properties: {
          privateDnsZoneId: openAiDnsZoneId
        }
      }
      {
        name: 'privatelink-services-ai'
        properties: {
          privateDnsZoneId: servicesAiDnsZoneId
        }
      }
    ]
  }
}

output privateEndpointId string = backendPe.id
output privateEndpointName string = backendPe.name
