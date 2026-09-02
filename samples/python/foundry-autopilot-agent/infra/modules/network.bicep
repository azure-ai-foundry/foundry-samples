param location string = resourceGroup().location
param tags object = {}

@description('Name of the hub virtual network')
param hubVirtualNetworkName string

@description('Address prefix for the hub virtual network')
param hubVirtualNetworkAddressPrefix string

@description('Address prefix for the AzureFirewallSubnet. Must be /26 or larger.')
param azureFirewallSubnetAddressPrefix string

@description('Name of the Foundry workload spoke virtual network')
param spokeVirtualNetworkName string

@description('Address prefix for the Foundry workload spoke virtual network')
param spokeVirtualNetworkAddressPrefix string

@description('Name of the Azure Firewall')
param firewallName string

@description('Name of the Azure Firewall policy')
param firewallPolicyName string

@description('Name of the route table applied to workload spoke subnets')
param spokeRouteTableName string

resource hubVirtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: hubVirtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        hubVirtualNetworkAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'AzureFirewallSubnet'
        properties: {
          addressPrefix: azureFirewallSubnetAddressPrefix
        }
      }
    ]
  }
}

resource spokeVirtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: spokeVirtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        spokeVirtualNetworkAddressPrefix
      ]
    }
  }
}

resource firewallPublicIp 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: '${firewallName}-pip'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
  }
}

resource firewallPolicy 'Microsoft.Network/firewallPolicies@2024-05-01' = {
  name: firewallPolicyName
  location: location
  tags: tags
  properties: {
    sku: {
      tier: 'Standard'
    }
    threatIntelMode: 'Alert'
  }
}

resource firewallPolicyRules 'Microsoft.Network/firewallPolicies/ruleCollectionGroups@2024-05-01' = {
  parent: firewallPolicy
  name: 'default-outbound-rules'
  properties: {
    priority: 100
    ruleCollections: [
      {
        name: 'allow-required-network-traffic'
        priority: 100
        ruleCollectionType: 'FirewallPolicyFilterRuleCollection'
        action: {
          type: 'Allow'
        }
        rules: [
          {
            name: 'allow-dns'
            ruleType: 'NetworkRule'
            ipProtocols: [
              'TCP'
              'UDP'
            ]
            sourceAddresses: [
              spokeVirtualNetworkAddressPrefix
            ]
            destinationAddresses: [
              '*'
            ]
            destinationPorts: [
              '53'
            ]
          }
          {
            name: 'allow-ntp'
            ruleType: 'NetworkRule'
            ipProtocols: [
              'UDP'
            ]
            sourceAddresses: [
              spokeVirtualNetworkAddressPrefix
            ]
            destinationAddresses: [
              '*'
            ]
            destinationPorts: [
              '123'
            ]
          }
        ]
      }
      {
        name: 'allow-https-outbound'
        priority: 200
        ruleCollectionType: 'FirewallPolicyFilterRuleCollection'
        action: {
          type: 'Allow'
        }
        rules: [
          {
            name: 'allow-https'
            ruleType: 'ApplicationRule'
            sourceAddresses: [
              spokeVirtualNetworkAddressPrefix
            ]
            protocols: [
              {
                protocolType: 'Https'
                port: 443
              }
            ]
            targetFqdns: [
              '*'
            ]
          }
        ]
      }
    ]
  }
}

resource azureFirewall 'Microsoft.Network/azureFirewalls@2024-05-01' = {
  name: firewallName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'AZFW_VNet'
      tier: 'Standard'
    }
    firewallPolicy: {
      id: firewallPolicy.id
    }
    ipConfigurations: [
      {
        name: 'firewall-ip-configuration'
        properties: {
          subnet: {
            id: resourceId('Microsoft.Network/virtualNetworks/subnets', hubVirtualNetwork.name, 'AzureFirewallSubnet')
          }
          publicIPAddress: {
            id: firewallPublicIp.id
          }
        }
      }
    ]
  }
  dependsOn: [
    firewallPolicyRules
  ]
}

resource spokeRouteTable 'Microsoft.Network/routeTables@2024-05-01' = {
  name: spokeRouteTableName
  location: location
  tags: tags
  properties: {
    disableBgpRoutePropagation: false
    routes: [
      {
        name: 'default-via-azure-firewall'
        properties: {
          addressPrefix: '0.0.0.0/0'
          nextHopType: 'VirtualAppliance'
          nextHopIpAddress: azureFirewall.properties.ipConfigurations[0].properties.privateIPAddress
        }
      }
    ]
  }
}

resource hubToSpokePeering 'Microsoft.Network/virtualNetworks/virtualNetworkPeerings@2024-05-01' = {
  parent: hubVirtualNetwork
  name: 'hub-to-${spokeVirtualNetwork.name}'
  properties: {
    allowVirtualNetworkAccess: true
    allowForwardedTraffic: true
    allowGatewayTransit: false
    useRemoteGateways: false
    remoteVirtualNetwork: {
      id: spokeVirtualNetwork.id
    }
  }
}

resource spokeToHubPeering 'Microsoft.Network/virtualNetworks/virtualNetworkPeerings@2024-05-01' = {
  parent: spokeVirtualNetwork
  name: 'spoke-to-${hubVirtualNetwork.name}'
  properties: {
    allowVirtualNetworkAccess: true
    allowForwardedTraffic: true
    allowGatewayTransit: false
    useRemoteGateways: false
    remoteVirtualNetwork: {
      id: hubVirtualNetwork.id
    }
  }
}

output hubVirtualNetworkId string = hubVirtualNetwork.id
output spokeVirtualNetworkId string = spokeVirtualNetwork.id
output spokeVirtualNetworkName string = spokeVirtualNetwork.name
output spokeRouteTableId string = spokeRouteTable.id
output firewallId string = azureFirewall.id
output firewallPrivateIpAddress string = azureFirewall.properties.ipConfigurations[0].properties.privateIPAddress
