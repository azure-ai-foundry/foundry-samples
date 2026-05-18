/*
Virtual Network Module
This module works with existing virtual networks and required subnets.

1. Flexibility:
   - Works with any existing VNet address space
   - Can use existing subnets or create new ones
   - Cross-resource group support

2. Security Features:
   - Network isolation
   - Subnet delegation for containerized workloads
   - Private endpoint subnet for secure connectivity
*/

@description('The name of the existing virtual network')
param vnetName string

@description('Subscription ID of virtual network (if different from current subscription)')
param vnetSubscriptionId string = subscription().subscriptionId

@description('Resource Group name of the existing VNet (if different from current resource group)')
param vnetResourceGroupName string = resourceGroup().name

@description('The name of Agents Subnet')
param agentSubnetName string = 'agent-subnet'

@description('The name of Private Endpoint subnet')
param peSubnetName string = 'pe-subnet'

@description('The name of MCP subnet for user-deployed Container Apps')
param mcpSubnetName string = 'mcp-subnet'

@description('Address prefix for the agent subnet (only needed if creating new subnet)')
param agentSubnetPrefix string = ''

@description('Address prefix for the private endpoint subnet (only needed if creating new subnet)')
param peSubnetPrefix string = ''

@description('Address prefix for the MCP subnet (only needed if creating new subnet)')
param mcpSubnetPrefix string = ''

@description('Set to true to reference the agent subnet as existing (do not create or modify it)')
param agentSubnetExists bool = false

@description('Set to true to reference the private endpoint subnet as existing (do not create or modify it)')
param peSubnetExists bool = false

@description('Set to true to reference the MCP subnet as existing (do not create or modify it)')
param mcpSubnetExists bool = false

// Get the address space (array of CIDR strings)
var vnetAddressSpace = existingVNet.properties.addressSpace.addressPrefixes[0]

var agentSubnetSpaces = empty(agentSubnetPrefix) ? cidrSubnet(vnetAddressSpace, 24, 0) : agentSubnetPrefix
var peSubnetSpaces = empty(peSubnetPrefix) ? cidrSubnet(vnetAddressSpace, 24, 1) : peSubnetPrefix
var mcpSubnetSpaces = empty(mcpSubnetPrefix) ? cidrSubnet(vnetAddressSpace, 24, 2) : mcpSubnetPrefix

// Reference the existing virtual network
resource existingVNet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: vnetName
  scope: resourceGroup(vnetResourceGroupName)
}

// Create (or reference) the agent subnet
// Subnet writes against the same VNet are serialized by ARM, so the three subnet
// modules below are explicitly chained via dependsOn to avoid AnotherOperationInProgress.
module agentSubnet 'subnet.bicep' = {
  name: 'agent-subnet-${uniqueString(deployment().name, agentSubnetName)}'
  scope: resourceGroup(vnetResourceGroupName)
  params: {
    vnetName: vnetName
    subnetName: agentSubnetName
    addressPrefix: agentSubnetSpaces
    subnetExists: agentSubnetExists
    delegations: [
      {
        name: 'Microsoft.App/environments'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
  }
}

// Create (or reference) the private endpoint subnet
module peSubnet 'subnet.bicep' = {
  name: 'pe-subnet-${uniqueString(deployment().name, peSubnetName)}'
  scope: resourceGroup(vnetResourceGroupName)
  params: {
    vnetName: vnetName
    subnetName: peSubnetName
    addressPrefix: peSubnetSpaces
    subnetExists: peSubnetExists
    delegations: []
  }
  dependsOn: [
    agentSubnet
  ]
}

// Create (or reference) the MCP subnet for user-deployed Container Apps
module mcpSubnet 'subnet.bicep' = {
  name: 'mcp-subnet-${uniqueString(deployment().name, mcpSubnetName)}'
  scope: resourceGroup(vnetResourceGroupName)
  params: {
    vnetName: vnetName
    subnetName: mcpSubnetName
    addressPrefix: mcpSubnetSpaces
    subnetExists: mcpSubnetExists
    delegations: [
      {
        name: 'Microsoft.App/environments'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
  }
  dependsOn: [
    peSubnet
  ]
}

// Output variables
output peSubnetName string = peSubnetName
output agentSubnetName string = agentSubnetName
output mcpSubnetName string = mcpSubnetName
// Use module outputs (not string concatenation) so consumers get an implicit dependency
// on the subnet modules and don't race ahead while subnets are still being written.
output agentSubnetId string = agentSubnet.outputs.subnetId
output peSubnetId string = peSubnet.outputs.subnetId
output mcpSubnetId string = mcpSubnet.outputs.subnetId
output virtualNetworkName string = existingVNet.name
output virtualNetworkId string = existingVNet.id
output virtualNetworkResourceGroup string = vnetResourceGroupName
output virtualNetworkSubscriptionId string = vnetSubscriptionId
