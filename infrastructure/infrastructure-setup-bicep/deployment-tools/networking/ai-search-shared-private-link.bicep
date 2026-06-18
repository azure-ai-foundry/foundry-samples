/*
  PATCH: AI Search → AI Services Shared Private Link
  ---------------------------------------------------
  Applies to: BYO Azure AI Search scenarios where publicNetworkAccess is Disabled
  on AI Services (the Foundry account).

  PROBLEM:
  Azure AI Search's vectorizer and indexer AI enrichment skills call AI Services
  outbound from AI Search's managed backend — which lives outside your VNet.
  When AI Services has publicNetworkAccess=Disabled, those calls are rejected.
  Private endpoints in your VNet only cover INBOUND traffic to AI Services.
  They do nothing for OUTBOUND calls from AI Search's managed infrastructure.

  SOLUTION:
  A Shared Private Link provisions a private endpoint FROM AI Search's managed
  infra INTO AI Services via Azure Private Link — no public access required.

  USAGE:
    az deployment group create \
      --resource-group <rg-where-ai-search-lives> \
      --template-file ai-search-shared-private-link.bicep \
      --parameters ai-search-shared-private-link.bicepparam

  AFTER DEPLOYMENT:
    Approve the pending private endpoint
    connection on the AI Services side. The link is not active until approved.

  SCOPE:
    Three SPLs are required for full Foundry coverage — all target the same AI Services
    resource ID but each uses a different groupId required by the AI Search SPL API:

      openai_account            — vectorizer (query-time embedding, integrated vectorization)
      cognitiveservices_account — built-in AI enrichment skills (OCR, entity extraction,
                                  key phrases) and their Foundry billing link
      foundry_account           — Azure-hosted model skills: GenAI prompt skill,
                                  Azure OpenAI embedding skill, Content Understanding skill

    NOTE: 'account' (the standard PE groupId for Cognitive Services) is NOT valid here —
          it causes: BadRequest: Cannot create private endpoint for requested type 'account'.

    Not needed: CosmosDB and Storage are passive data stores — they never initiate
                outbound calls, so no shared private link is required for them.
*/

@minLength(1)
@description('Name of the Azure AI Search service (BYO resource).')
param aiSearchName string

@minLength(1)
@description('Full resource ID of the Azure AI Services / Foundry account to connect to. Format: /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<name>')
param aiServicesResourceId string

@description('Prefix for SPL resource names. Change this if you need multiple SPL sets targeting different AI Services accounts from the same AI Search instance.')
param splNamePrefix string = 'foundry-spl'

// ---------------------------------------------------------------------------
// Reference the existing AI Search service in the deployment resource group
// ---------------------------------------------------------------------------
resource searchService 'Microsoft.Search/searchServices@2024-03-01-preview' existing = {
  name: aiSearchName
}

// ---------------------------------------------------------------------------
// SPL 1 — openai_account
// Covers: vectorizer (query-time embedding via integrated vectorization)
// ---------------------------------------------------------------------------
resource splOpenAI 'Microsoft.Search/searchServices/sharedPrivateLinkResources@2024-03-01-preview' = {
  parent: searchService
  name: '${splNamePrefix}-openai'
  properties: {
    privateLinkResourceId: aiServicesResourceId
    groupId: 'openai_account'
    requestMessage: 'AI Search vectorizer requires private access to AI Services (openai_account)'
  }
}

// ---------------------------------------------------------------------------
// SPL 2 — cognitiveservices_account
// Covers: built-in AI enrichment skills (OCR, entity extraction, etc.) and
//         the Foundry billing link used by skillsets
// dependsOn splOpenAI: AI Search only accepts one SPL write at a time
// ---------------------------------------------------------------------------
resource splCogSvc 'Microsoft.Search/searchServices/sharedPrivateLinkResources@2024-03-01-preview' = {
  parent: searchService
  name: '${splNamePrefix}-cogsvc'
  properties: {
    privateLinkResourceId: aiServicesResourceId
    groupId: 'cognitiveservices_account'
    requestMessage: 'AI Search enrichment skills require private access to AI Services (cognitiveservices_account)'
  }
  dependsOn: [splOpenAI]
}

// ---------------------------------------------------------------------------
// SPL 3 — foundry_account
// Covers: Azure-hosted model skills — GenAI prompt skill, Azure OpenAI
//         embedding skill, Content Understanding skill
// dependsOn splCogSvc: AI Search only accepts one SPL write at a time
// ---------------------------------------------------------------------------
resource splFoundry 'Microsoft.Search/searchServices/sharedPrivateLinkResources@2024-03-01-preview' = {
  parent: searchService
  name: '${splNamePrefix}-foundry'
  properties: {
    privateLinkResourceId: aiServicesResourceId
    groupId: 'foundry_account'
    requestMessage: 'AI Search model skills require private access to AI Services (foundry_account)'
  }
  dependsOn: [splCogSvc]
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Provisioning state of the openai_account SPL (vectorizer).')
output splOpenAIState string = splOpenAI.properties.status

@description('Provisioning state of the cognitiveservices_account SPL (enrichment skills).')
output splCogSvcState string = splCogSvc.properties.status

@description('Provisioning state of the foundry_account SPL (hosted model skills).')
output splFoundryState string = splFoundry.properties.status
