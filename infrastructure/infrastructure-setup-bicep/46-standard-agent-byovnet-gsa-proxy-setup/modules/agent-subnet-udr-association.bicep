// ---- Agent Subnet UDR Association Module ----
// Updates the existing agent subnet to associate it with the route table
// that routes 0/0 traffic through the GSA proxy.
// IMPORTANT: Preserves the existing subnet delegation (e.g., Microsoft.App/environments)
// and NSG association when adding the route table.

@description('Name of the virtual network')
param vnetName string

@description('Name of the agent subnet')
param agentSubnetName string

@description('Address prefix of the agent subnet')
param agentSubnetAddressPrefix string

@description('Resource ID of the existing NSG on the agent subnet (empty string if none)')
param existingNsgId string

@description('Existing delegations on the agent subnet to preserve')
param existingDelegations array = []

@description('Resource ID of the route table to associate')
param routeTableId string

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' existing = {
  name: vnetName
}

resource agentSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' = {
  parent: vnet
  name: agentSubnetName
  properties: {
    addressPrefix: agentSubnetAddressPrefix
    networkSecurityGroup: !empty(existingNsgId) ? {
      id: existingNsgId
    } : null
    delegations: existingDelegations
    routeTable: {
      id: routeTableId
    }
  }
}
