/*
  vnet-with-backend-subnet.bicep
  ------------------------------
  Creates all extension-owned subnets in one VNet update for new networks.
  Existing customer VNets retain additive child-subnet updates.
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

@description('Subnet name and CIDR for APIM Standard v2 outbound integration.')
param apimOutboundSubnetName string = 'apim-outbound'
param apimOutboundSubnetPrefix string

var vnetAddress = empty(vnetAddressPrefix) ? '192.168.0.0/16' : vnetAddressPrefix
var agentSubnetAddress = empty(agentSubnetPrefix) ? cidrSubnet(vnetAddress, 24, 0) : agentSubnetPrefix
var peSubnetAddress = empty(peSubnetPrefix) ? cidrSubnet(vnetAddress, 24, 1) : peSubnetPrefix

resource apimOutboundNsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = if (!useExistingVnet) {
  name: '${apimOutboundSubnetName}-nsg'
  location: location
}

resource newVnet 'Microsoft.Network/virtualNetworks@2024-05-01' = if (!useExistingVnet) {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [vnetAddress]
    }
    subnets: [
      {
        name: agentSubnetName
        properties: {
          addressPrefix: agentSubnetAddress
          delegations: [
            {
              name: 'Microsoft.app/environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: peSubnetName
        properties: {
          addressPrefix: peSubnetAddress
          privateEndpointNetworkPolicies: 'Disabled'
          defaultOutboundAccess: false
        }
      }
      {
        name: backendPeSubnetName
        properties: {
          addressPrefix: backendPeSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
          defaultOutboundAccess: false
        }
      }
      {
        name: apimOutboundSubnetName
        properties: {
          addressPrefix: apimOutboundSubnetPrefix
          networkSecurityGroup: {
            id: apimOutboundNsg.id
          }
          defaultOutboundAccess: false
          delegations: [
            {
              name: 'Microsoft.Web/serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

module existingVnet '../../../modules-network-secured/network-agent-vnet.bicep' = if (useExistingVnet) {
  name: 'base-vnet-${vnetName}-deployment'
  scope: resourceGroup(existingVnetSubscriptionId, existingVnetResourceGroupName)
  params: {
    location: location
    vnetName: vnetName
    useExistingVnet: true
    existingVnetSubscriptionId: existingVnetSubscriptionId
    existingVnetResourceGroupName: existingVnetResourceGroupName
    agentSubnetName: agentSubnetName
    peSubnetName: peSubnetName
    vnetAddressPrefix: vnetAddressPrefix
    agentSubnetPrefix: agentSubnetPrefix
    peSubnetPrefix: peSubnetPrefix
  }
}

module existingExtensionSubnets 'existing-vnet-extension-subnets.bicep' = if (useExistingVnet) {
  name: 'extension-subnets-${vnetName}-deployment'
  scope: resourceGroup(existingVnetSubscriptionId, existingVnetResourceGroupName)
  params: {
    location: location
    vnetName: vnetName
    backendPeSubnetName: backendPeSubnetName
    backendPeSubnetPrefix: backendPeSubnetPrefix
    apimOutboundSubnetName: apimOutboundSubnetName
    apimOutboundSubnetPrefix: apimOutboundSubnetPrefix
  }
  dependsOn: [
    existingVnet
  ]
}

var newVnetId = resourceId('Microsoft.Network/virtualNetworks', vnetName)
var effectiveVnetId = useExistingVnet ? existingVnet.outputs.virtualNetworkId : newVnetId

output virtualNetworkName string = useExistingVnet ? existingVnet.outputs.virtualNetworkName : vnetName
output virtualNetworkId string = effectiveVnetId
output virtualNetworkResourceGroup string = useExistingVnet ? existingVnet.outputs.virtualNetworkResourceGroup : resourceGroup().name
output virtualNetworkSubscriptionId string = useExistingVnet ? existingVnet.outputs.virtualNetworkSubscriptionId : subscription().subscriptionId
output agentSubnetId string = useExistingVnet ? existingVnet.outputs.agentSubnetId : '${newVnetId}/subnets/${agentSubnetName}'
output agentSubnetName string = agentSubnetName
output peSubnetId string = useExistingVnet ? existingVnet.outputs.peSubnetId : '${newVnetId}/subnets/${peSubnetName}'
output peSubnetName string = peSubnetName
output backendPeSubnetId string = useExistingVnet ? existingExtensionSubnets.outputs.backendPeSubnetId : '${newVnetId}/subnets/${backendPeSubnetName}'
output backendPeSubnetName string = backendPeSubnetName
output apimOutboundSubnetId string = useExistingVnet ? existingExtensionSubnets.outputs.apimOutboundSubnetId : '${newVnetId}/subnets/${apimOutboundSubnetName}'
