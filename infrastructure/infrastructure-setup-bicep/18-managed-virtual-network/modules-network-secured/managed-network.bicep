@description('The name of the AI Services account')
param accountName string

@description('The isolation mode for the managed network')
@allowed([
  'AllowOnlyApprovedOutbound'
  'AllowInternetOutbound'
])
param isolationMode string = 'AllowOnlyApprovedOutbound'

// Reference the existing AI Services account in the same resource group
resource aiAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

// Create the managed network settings first
#disable-next-line BCP081
resource managedNetwork 'Microsoft.CognitiveServices/accounts/managednetworks@2025-10-01-preview' = {
  parent: aiAccount
  name: 'default'
  properties: {
    managedNetwork: {
      IsolationMode: isolationMode
      managedNetworkKind: 'V2'
      provisionNetworkNow: true
      //firewallSku: 'Standard' // Uncomment to enable firewall only when in AllowOnlyApprovedOutbound mode
    }
  }
}

output managedNetworkSettingsName string = managedNetwork.name
