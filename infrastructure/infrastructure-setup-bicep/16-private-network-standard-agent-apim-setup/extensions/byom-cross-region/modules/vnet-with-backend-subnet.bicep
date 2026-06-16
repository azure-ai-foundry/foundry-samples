/*
  vnet-with-backend-subnet.bicep
  ------------------------------
  Thin extension of template 16's network module that adds a THIRD subnet
  (backend-pe) to host the cross-region private endpoint to the backend
  Foundry account. Everything else (agent-subnet delegation, pe-subnet,
  existing-vnet support) is inherited from template 16.

  This module is intentionally a wrapper instead of a fork — it keeps
  template 16's behavior the source of truth for the project-side VNet,
  and only adds the new backend-pe subnet on top of it.
*/

@description('Azure region for the VNet (must equal the project region).')
param location string

@description('Name of the virtual network. Created if it does not exist.')
param vnetName string

@description('Whether to use an existing VNet instead of creating one.')
param useExistingVnet bool = false

@description('Subscription ID of the existing VNet if different from current.')
param existingVnetSubscriptionId string = subscription().subscriptionId

@description('Resource group of the existing VNet if different from current.')
param existingVnetResourceGroupName string = resourceGroup().name

@description('Address space for the VNet. Required when useExistingVnet is false.')
param vnetAddressPrefix string = ''

@description('Subnet name and CIDR for the Foundry agent subnet (delegated to Microsoft.App/environments).')
param agentSubnetName string = 'agent-subnet'
param agentSubnetPrefix string = ''

@description('Subnet name and CIDR for in-region private endpoints (Foundry account, Storage, Cosmos, AI Search, APIM).')
param peSubnetName string = 'pe-subnet'
param peSubnetPrefix string = ''

@description('Subnet name and CIDR for the cross-region private endpoint to the backend Foundry account.')
param backendPeSubnetName string = 'backend-pe'
param backendPeSubnetPrefix string

// Delegate base VNet + agent-subnet + pe-subnet to template 16's module.
module baseVnet '../../../modules-network-secured/network-agent-vnet.bicep' = {
  name: 'base-vnet-${vnetName}-deployment'
  params: {
    location: location
    vnetName: vnetName
    useExistingVnet: useExistingVnet
    existingVnetSubscriptionId: existingVnetSubscriptionId
    existingVnetResourceGroupName: existingVnetResourceGroupName
    agentSubnetName: agentSubnetName
    peSubnetName: peSubnetName
    vnetAddressPrefix: vnetAddressPrefix
    agentSubnetPrefix: agentSubnetPrefix
    peSubnetPrefix: peSubnetPrefix
  }
}

// Add the backend-pe subnet to whatever VNet we ended up with. Using an
// independent subnet resource so we do not race with the parent vnet
// module on subnet collection writes.
resource backendPeSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  name: '${vnetName}/${backendPeSubnetName}'
  properties: {
    addressPrefix: backendPeSubnetPrefix
    privateEndpointNetworkPolicies: 'Disabled'
    // The backend-pe subnet only hosts the cross-region private endpoint
    // into the backend Foundry account; nothing in it ever initiates
    // outbound traffic. Disabling default outbound access avoids relying
    // on the deprecated implicit egress path and complies with subscription
    // guardrails requiring defaultOutboundAccess=false on every subnet.
    defaultOutboundAccess: false
  }
  dependsOn: [
    baseVnet
  ]
}

output virtualNetworkName string = baseVnet.outputs.virtualNetworkName
output virtualNetworkId string = baseVnet.outputs.virtualNetworkId
output virtualNetworkResourceGroup string = baseVnet.outputs.virtualNetworkResourceGroup
output virtualNetworkSubscriptionId string = baseVnet.outputs.virtualNetworkSubscriptionId
output agentSubnetId string = baseVnet.outputs.agentSubnetId
output agentSubnetName string = baseVnet.outputs.agentSubnetName
output peSubnetId string = baseVnet.outputs.peSubnetId
output peSubnetName string = baseVnet.outputs.peSubnetName
output backendPeSubnetId string = backendPeSubnet.id
output backendPeSubnetName string = backendPeSubnetName
