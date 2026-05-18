@description('Name of the virtual network')
param vnetName string

@description('Name of the subnet')
param subnetName string

@description('Address prefix for the subnet (only required when creating a new subnet)')
param addressPrefix string = ''

@description('Array of subnet delegations')
param delegations array = []

@description('Set to true to reference an existing subnet instead of creating one')
param subnetExists bool = false

resource newSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = if (!subnetExists) {
  name: '${vnetName}/${subnetName}'
  properties: {
    addressPrefix: addressPrefix
    delegations: delegations
  }
}

resource existingSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = if (subnetExists) {
  name: '${vnetName}/${subnetName}'
}

output subnetId string = subnetExists ? existingSubnet.id : newSubnet.id
output subnetName string = subnetName
