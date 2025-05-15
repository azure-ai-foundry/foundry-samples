/*
Virtual Network Module
This module deploys the core network infrastructure with security controls:

1. Address Space:
   - VNet CIDR: 172.16.0.0/16
   - Hub Subnet: 172.16.0.0/24 (private endpoints)
   - Agents Subnet: 172.16.101.0/24 (container apps)

2. Security Features:
   - Service endpoints
   - Network isolation
   - Subnet delegation
*/

@description('Azure region for the deployment')
param location string

@description('The name of the virtual network')
param vnetName string = 'agents-vnet-test'

@description('The name of Agents Subnet')
param agentSubnetName string = 'default'

@description('The name of Hub subnet')
param peSubnetName string = 'pe-subnet'


resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '192.168.0.0/16'
      ]
    }
    subnets: [
      {
        name: agentSubnetName
        properties: {
          addressPrefix: '192.168.0.0/24'
        }
      }
      {
        name: peSubnetName
        properties: {
          addressPrefix: '192.168.1.0/24'
        }
      }
    ]
  }
}

resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: virtualNetwork
  name: agentSubnetName
  properties: {
    addressPrefix: '192.168.0.0/24'
	delegations: [
      {
        id: '${virtualNetwork.id}/subnets/${agentSubnetName}'
        name: 'Microsoft.App/environments'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
        type: 'Microsoft.Network/virtualNetworks/subnets/delegations'
      }
    ]
  }
}
// Output variables
output peSubnetName string = peSubnetName
output agentSubnetName string = agentSubnetName
output subnetId string = subnet.id
output virtualNetworkName string = virtualNetwork.name
