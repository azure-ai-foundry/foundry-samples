/*
Project-level capability host (Agents kind) for the evaluation/datagen template.
No BYO connections: this template has no Cosmos DB, AI Search, or storage backend
for the host. The account-level capability host is auto-created via the account's
networkInjections.scenario='agent' (see ai-account-identity.bicep).
*/

@description('Name of the AI Foundry (Cognitive Services) account.')
param accountName string

@description('Name of the project to create the capability host under.')
param projectName string

@description('Name of the project capability host to create.')
param projectCapHost string

resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' existing = {
  name: projectName
  parent: account
}

resource projectCapabilityHost 'Microsoft.CognitiveServices/accounts/projects/capabilityHosts@2025-04-01-preview' = {
  name: projectCapHost
  parent: project
  properties: {
    // Bicep type definitions for capabilityHosts are stale and reject
    // `capabilityHostKind`, but the ARM API REQUIRES it (without it the
    // capability host is created with no kind and downstream agents fail).
    // Suppressing the false-positive BCP037 since runtime validation passes.
    #disable-next-line BCP037
    capabilityHostKind: 'Agents'
  }
}

output projectCapHost string = projectCapabilityHost.name
