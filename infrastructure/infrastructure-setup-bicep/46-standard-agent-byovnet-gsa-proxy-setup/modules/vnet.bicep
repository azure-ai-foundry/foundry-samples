// ---- Virtual Network with Agent Subnet ----
// Creates a VNet with a single agent subnet delegated to Microsoft.App/environments.
// No PE subnet is needed since this setup does not use private endpoints.

@description('Azure region for the VNet')
param location string

@description('Name of the virtual network')
param vnetName string

@description('Address space for the VNet')
param vnetAddressPrefix string = '10.0.0.0/16'

@description('Name of the agent subnet')
param agentSubnetName string = 'agent-subnet'

@description('Address prefix for the agent subnet (recommended /24)')
param agentSubnetPrefix string = '10.0.0.0/24'

@description('Name of the GSA proxy subnet')
param gsaProxySubnetName string = 'gsa-proxy-subnet'

@description('Address prefix for the GSA proxy subnet')
param gsaProxySubnetPrefix string = '10.0.1.0/24'

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: agentSubnetName
        properties: {
          addressPrefix: agentSubnetPrefix
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: gsaProxySubnetName
        properties: {
          addressPrefix: gsaProxySubnetPrefix
        }
      }
    ]
  }
}

output virtualNetworkName string = vnet.name
output virtualNetworkId string = vnet.id
output agentSubnetName string = agentSubnetName
output agentSubnetId string = '${vnet.id}/subnets/${agentSubnetName}'
output gsaProxySubnetName string = gsaProxySubnetName
output gsaProxySubnetId string = '${vnet.id}/subnets/${gsaProxySubnetName}'
