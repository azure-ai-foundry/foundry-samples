using './add-existing-project.bicep'

// EXISTING project to reuse in place (no suffix is appended).
param projectName = 'your-existing-project-name'
param projectCapHost = 'caphostproj'

// Set false to skip every role-assignment module. Use this only when the project
// identity is ALREADY permissioned on Storage, Cosmos DB, and AI Search (for
// example a production project whose roles were granted earlier under different
// assignment names), to avoid a RoleAssignmentExists conflict.
// param assignRoles = true

// Optional. Override the connection names the capability host binds to. Leave
// empty (the default) to use <resourceName>-<project>. Set these to match
// connections that a pre-existing capability host already references.
// param cosmosDBConnectionName = ''
// param azureStorageConnectionName = ''
// param aiSearchConnectionName = ''

// Existing AI Services (Foundry) account details
param existingAccountName = '' // Replace with your actual account name
param accountResourceGroupName = '' // Your resource group
param accountSubscriptionId = ''

// Existing shared resources (from your original deployment)
param existingAiSearchName = '' // Replace with your actual search service name
param aiSearchResourceGroupName = '' // Your resource group
param aiSearchSubscriptionId = ''

param existingStorageName = '' // Replace with your actual storage account name
param storageResourceGroupName = '' // Your resource group
param storageSubscriptionId = ''

param existingCosmosDBName = '' // Replace with your actual Cosmos DB name
param cosmosDBResourceGroupName = '' // Your resource group
param cosmosDBSubscriptionId = ''
