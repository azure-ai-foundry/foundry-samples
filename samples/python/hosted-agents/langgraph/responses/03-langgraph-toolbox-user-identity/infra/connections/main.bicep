targetScope = 'subscription'

@description('Name of the resource group containing the Microsoft Foundry account.')
param resourceGroupName string

@description('Name of the existing Microsoft Foundry account.')
param accountName string

module connections 'connections.bicep' = {
  name: 'toolbox-user-identity-connections'
  scope: resourceGroup(resourceGroupName)
  params: {
    accountName: accountName
  }
}
