param accountName string
param location string = resourceGroup().location
param tags object = {}
param cognitiveServicesSku string = 'S0'

param virtualNetworkName string
param spokeVirtualNetworkAddressPrefix string
param agentSubnetAddressPrefix string
param privateEndpointSubnetAddressPrefix string
param virtualMachineSubnetAddressPrefix string
param hubVirtualNetworkName string
param hubVirtualNetworkAddressPrefix string
param azureFirewallSubnetAddressPrefix string
param firewallName string
param firewallPolicyName string
param spokeRouteTableName string

var agentSubnetName = 'agent-subnet'
var privateEndpointSubnetName = 'private-endpoint-subnet'
var virtualMachineSubnetName = 'virtual-machine-subnet'
var agentSubnetResourceId = resourceId('Microsoft.Network/virtualNetworks/subnets', virtualNetworkName, agentSubnetName)
var privateEndpointSubnetResourceId = resourceId('Microsoft.Network/virtualNetworks/subnets', virtualNetworkName, privateEndpointSubnetName)
var virtualMachineSubnetResourceId = resourceId('Microsoft.Network/virtualNetworks/subnets', virtualNetworkName, virtualMachineSubnetName)
var teamsIpv4AddressPrefixes = [
  '52.112.0.0/14'
  '52.122.0.0/15'
]
var privateDnsZoneNames = [
  'privatelink.services.ai.azure.com'
  'privatelink.openai.azure.com'
  'privatelink.cognitiveservices.azure.com'
]

module network 'network.bicep' = {
  name: 'hub-spoke-network-deployment'
  params: {
    location: location
    tags: tags
    hubVirtualNetworkName: hubVirtualNetworkName
    hubVirtualNetworkAddressPrefix: hubVirtualNetworkAddressPrefix
    azureFirewallSubnetAddressPrefix: azureFirewallSubnetAddressPrefix
    spokeVirtualNetworkName: virtualNetworkName
    spokeVirtualNetworkAddressPrefix: spokeVirtualNetworkAddressPrefix
    firewallName: firewallName
    firewallPolicyName: firewallPolicyName
    spokeRouteTableName: spokeRouteTableName
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: virtualNetworkName
}

resource agentSubnetNetworkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: '${virtualNetworkName}-agent-nsg'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowPrivateEndpointsOutbound'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'VirtualNetwork'
        }
      }
      {
        name: 'AllowMicrosoftEntraOutbound'
        properties: {
          priority: 110
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'AzureActiveDirectory'
        }
      }
      {
        name: 'AllowAzureMonitorOutbound'
        properties: {
          priority: 120
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'AzureMonitor'
        }
      }
      {
        name: 'AllowAzureFrontDoorOutbound'
        properties: {
          priority: 130
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'AzureFrontDoor.Frontend'
        }
      }
      {
        name: 'AllowMicrosoftTeamsOutbound'
        properties: {
          priority: 140
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefixes: teamsIpv4AddressPrefixes
        }
      }
      {
        name: 'DenyAllOutbound'
        properties: {
          priority: 4096
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

resource agentSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: virtualNetwork
  name: agentSubnetName
  properties: {
    addressPrefix: agentSubnetAddressPrefix
    privateEndpointNetworkPolicies: 'Disabled'
    networkSecurityGroup: {
      id: agentSubnetNetworkSecurityGroup.id
    }
    routeTable: {
      id: network.outputs.spokeRouteTableId
    }
    delegations: [
      {
        name: 'Microsoft.App-environments'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
  }
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: virtualNetwork
  name: privateEndpointSubnetName
  properties: {
    addressPrefix: privateEndpointSubnetAddressPrefix
    privateEndpointNetworkPolicies: 'Disabled'
    routeTable: {
      id: network.outputs.spokeRouteTableId
    }
  }
}

resource virtualMachineSubnetNetworkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: '${virtualNetworkName}-vm-nsg'
  location: location
  tags: tags
}

resource virtualMachineSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: virtualNetwork
  name: virtualMachineSubnetName
  properties: {
    addressPrefix: virtualMachineSubnetAddressPrefix
    privateEndpointNetworkPolicies: 'Enabled'
    networkSecurityGroup: {
      id: virtualMachineSubnetNetworkSecurityGroup.id
    }
    routeTable: {
      id: network.outputs.spokeRouteTableId
    }
  }
}

resource account 'Microsoft.CognitiveServices/accounts@2025-09-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: cognitiveServicesSku
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Disabled'
    allowProjectManagement: true
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: []
    }
    networkInjections: [
      {
        scenario: 'agent'
        subnetArmId: agentSubnetResourceId
        useMicrosoftManagedNetwork: false
      }
    ]
  }
  dependsOn: [
    agentSubnet
  ]
}

resource privateDnsZones 'Microsoft.Network/privateDnsZones@2020-06-01' = [for zoneName in privateDnsZoneNames: {
  name: zoneName
  location: 'global'
  tags: tags
}]

resource privateDnsZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = [for (zoneName, i) in privateDnsZoneNames: {
  parent: privateDnsZones[i]
  name: '${replace(zoneName, '.', '-')}-${uniqueString(virtualNetwork.id)}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    resolutionPolicy: 'Default'
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}]

resource accountPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${accountName}-private-endpoint'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetResourceId
    }
    privateLinkServiceConnections: [
      {
        name: '${accountName}-private-link-service-connection'
        properties: {
          privateLinkServiceId: account.id
          groupIds: [
            'account'
          ]
        }
      }
    ]
  }
  dependsOn: [
    privateEndpointSubnet
  ]
}

resource accountPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: accountPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [for (zoneName, i) in privateDnsZoneNames: {
      name: replace(zoneName, '.', '-')
      properties: {
        privateDnsZoneId: privateDnsZones[i].id
      }
    }]
  }
  dependsOn: [
    privateDnsZoneLinks
  ]
}

output accountId string = account.id
output virtualNetworkId string = virtualNetwork.id
output hubVirtualNetworkId string = network.outputs.hubVirtualNetworkId
output firewallId string = network.outputs.firewallId
output firewallPrivateIpAddress string = network.outputs.firewallPrivateIpAddress
output spokeRouteTableId string = network.outputs.spokeRouteTableId
output agentSubnetId string = agentSubnetResourceId
output virtualMachineSubnetId string = virtualMachineSubnetResourceId
output privateEndpointId string = accountPrivateEndpoint.id
