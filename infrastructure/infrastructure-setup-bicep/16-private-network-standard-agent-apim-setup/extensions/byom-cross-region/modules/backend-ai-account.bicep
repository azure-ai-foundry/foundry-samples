/*
  backend-ai-account.bicep
  ------------------------
  Backend Microsoft Foundry (Cognitive Services / AIServices) account in
  a SECOND region. Hosts the model deployments (gpt-4o, gpt-5, gpt-5.1)
  that the project-region Foundry account does not have access to today —
  for example, frontier models that land in japaneast before canadaeast.

  Network posture:
    publicNetworkAccess: 'Disabled'  — the only path in is the
    cross-region private endpoint deployed by backend-private-endpoint.bicep.

  Authentication:
    System-assigned MI on the account (so APIM's set-backend-service +
    authentication-managed-identity policy chain can mint a token against
    it). disableLocalAuth is true — only Entra tokens are accepted, which
    matches the org guardrail "no shared keys on Cognitive Services" and
    closes the local-auth escape hatch entirely. APIM forwards Entra
    tokens in the Authorization header, so the path remains fully
    functional with keys disabled.
*/

@description('Backend region — distinct from the project region. Example: japaneast when the project lives in canadaeast.')
param location string

@description('Name of the backend Foundry account. Must be globally unique. 2-64 chars, no dots.')
param accountName string

@description('Array of model deployments to create on the backend account. Each entry shape: { name, format, version, skuName, capacity }.')
param modelDeployments array

resource backendAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: accountName
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    allowProjectManagement: false
    customSubDomainName: accountName
    networkAcls: {
      defaultAction: 'Deny'
      virtualNetworkRules: []
      ipRules: []
      bypass: 'AzureServices'
    }
    publicNetworkAccess: 'Disabled'
    disableLocalAuth: true
  }
}

@batchSize(1)
resource backendDeployments 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = [for deployment in modelDeployments: {
  parent: backendAccount
  name: deployment.name
  sku: {
    name: deployment.skuName
    capacity: deployment.capacity
  }
  properties: {
    model: {
      name: deployment.name
      format: deployment.format
      version: deployment.version
    }
  }
}]

output accountName string = backendAccount.name
output accountId string = backendAccount.id
output accountEndpoint string = backendAccount.properties.endpoint
output accountPrincipalId string = backendAccount.identity.principalId
