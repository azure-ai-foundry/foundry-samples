// Account-level capabilityHost.
//
// The current template ONLY creates the project-level capabilityHost in Bicep
// and relies on the `createCapHost.sh` script being run manually before
// deployment to bootstrap the account-level capabilityHost. When the account
// is BYO (already-existing Foundry account that never had the script run),
// the project capability host creation fails with:
//   "Foundry Account capabilityHost Not Found, please retry again after
//    creating capabilityHost for the Foundry Account."
//
// This module bootstraps the account-level capabilityHost in Bicep so the
// flow is fully declarative and idempotent regardless of whether the account
// is freshly created or pre-existing.

@description('Name of the AI Foundry (Cognitive Services) account')
param accountName string

@description('Name of the account-level capabilityHost')
param accountCapHost string = 'caphostacct'

@description('ARM resource ID of the customer agent subnet (delegated to Microsoft.App/environments)')
param agentSubnetResourceId string

resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

resource accountCapabilityHost 'Microsoft.CognitiveServices/accounts/capabilityHosts@2025-04-01-preview' = {
  name: accountCapHost
  parent: account
  properties: {
    // Bicep type defs reject this property; ARM API requires it.
    #disable-next-line BCP037
    capabilityHostKind: 'Agents'
    #disable-next-line BCP037
    customerSubnet: agentSubnetResourceId
  }
}

output accountCapHostName string = accountCapabilityHost.name
