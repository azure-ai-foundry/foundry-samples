using './ai-search-shared-private-link.bicep'

// Name of the BYO AI Search service
param aiSearchName = '<ai-search-service-name>'

// Full resource ID of the Foundry AI Services account (with unique suffix)
// Three SPLs are deployed automatically targeting this resource:
//   <prefix>-openai  (openai_account)          — vectorizer
//   <prefix>-cogsvc  (cognitiveservices_account) — enrichment skills + billing
//   <prefix>-foundry (foundry_account)           — hosted model skills
param aiServicesResourceId = '/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.CognitiveServices/accounts/<foundry-resource-name>'

// SPL name prefix — change only if you connect multiple AI Services accounts to the same AI Search
// param splNamePrefix = 'foundry-spl'
