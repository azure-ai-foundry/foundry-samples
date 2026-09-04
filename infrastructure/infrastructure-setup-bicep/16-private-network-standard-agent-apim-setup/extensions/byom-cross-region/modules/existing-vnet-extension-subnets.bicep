@description('Azure region of the existing VNet.')
param location string

@description('Name of the existing VNet.')
param vnetName string

@description('Subnet name and CIDR for the backend Foundry private endpoint.')
param backendPeSubnetName string
param backendPeSubnetPrefix string

@description('Subnet name and CIDR for APIM Standard v2 outbound integration.')
param apimOutboundSubnetName string
param apimOutboundSubnetPrefix string

resource apimOutboundNsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: '${apimOutboundSubnetName}-nsg'
  location: location
}

resource backendPeSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  name: '${vnetName}/${backendPeSubnetName}'
  properties: {
    addressPrefix: backendPeSubnetPrefix
    privateEndpointNetworkPolicies: 'Disabled'
    defaultOutboundAccess: false
  }
}

resource apimOutboundSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  name: '${vnetName}/${apimOutboundSubnetName}'
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
  dependsOn: [
    backendPeSubnet
  ]
}

output backendPeSubnetId string = backendPeSubnet.id
output apimOutboundSubnetId string = apimOutboundSubnet.id
