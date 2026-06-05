// Account-level capability host.
//
// Only one capability host per Foundry account is allowed. For a fresh account
// with `networkInjections.scenario='agent'`, the platform auto-creates one
// named `<account>@aml_aiagentservice` — this module is NOT needed.
//
// Use this module only when the account has NO capability host:
//   - BYO account that never had one created, or
//   - After running `deleteCapHost.sh` for a redeploy.
//
// Default `accountCapHost` matches the platform convention so the resulting
// caphost is named the same as the implicit one would have been.

@description('Name of the AI Foundry (Cognitive Services) account')
param accountName string

@description('Name of the account-level capability host. Defaults to the platform convention `<account>@aml_aiagentservice`.')
param accountCapHost string = '${accountName}@aml_aiagentservice'

@description('ARM resource ID of the customer agent subnet')
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
