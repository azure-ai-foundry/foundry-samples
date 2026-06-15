/*
Connections enable your AI applications to access tools and objects managed elsewhere in or outside of Azure.

This example demonstrates how to add an Azure Storage connection.

It uses Microsoft Entra ID (AAD) authentication instead of account keys, creates the
connection on both the Microsoft Foundry account and the project, and assigns the project's
system-assigned managed identity the Storage Blob Data Contributor role on the storage account.
*/
param aiFoundryName string = '<your-account-name>'

@description('Name of the project (sub-resource of the AI Foundry account) to create the connection on.')
param aiProjectName string = '<your-project-name>'

param connectedResourceName string = 'st${aiFoundryName}'
param location string = 'westus'

// Share connection with all users
param isSharedToAll bool = true

// Whether to create a new Azure Storage account
@allowed([
  'new'
  'existing'
])
param newOrExisting string = 'new'

// Refers your existing Microsoft Foundry resource
resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: aiFoundryName
  scope: resourceGroup()
}

// Refers your existing project (sub-resource of the AI Foundry account)
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' existing = {
  name: aiProjectName
  parent: aiFoundry
}

// Conditionally creates a new Azure Storage account
resource newStorage 'Microsoft.Storage/storageAccounts@2024-01-01' = if (newOrExisting == 'new') {
  name: connectedResourceName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    // Disable shared key access so only Entra ID (AAD) auth is used
    allowSharedKeyAccess: false
  }
}

// Normalized reference to the target storage account (works for both new and existing)
resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: connectedResourceName
}

// Creates the Azure Foundry account-level connection to your Azure Storage account
resource accountConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  name: '${aiFoundryName}-storage'
  parent: aiFoundry
  properties: {
    category: 'AzureStorageAccount'
    target: storageAccount.properties.primaryEndpoints.blob
    authType: 'AAD'
    isSharedToAll: isSharedToAll
    metadata: {
      ApiType: 'Azure'
      ResourceId: storageAccount.id
      location: storageAccount.location
    }
  }
  dependsOn: [
    newStorage
  ]
}

// Creates the project-level connection to your Azure Storage account
resource projectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = {
  name: connectedResourceName
  parent: aiProject
  properties: {
    category: 'AzureStorageAccount'
    target: storageAccount.properties.primaryEndpoints.blob
    authType: 'AAD'
    isSharedToAll: isSharedToAll
    metadata: {
      ApiType: 'Azure'
      ResourceId: storageAccount.id
      location: storageAccount.location
    }
  }
  dependsOn: [
    newStorage
  ]
}

// Storage Blob Data Contributor: ba92f5b4-2d11-453d-a403-e96b0029c9fe
resource storageBlobDataContributor 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  scope: resourceGroup()
}

// Assigns the project's system-assigned managed identity the Storage Blob Data Contributor
// role on the storage account so it can access blobs using Entra ID (AAD) auth.
resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(aiProject.id, storageBlobDataContributor.id, storageAccount.id)
  properties: {
    principalId: aiProject.identity.principalId
    roleDefinitionId: storageBlobDataContributor.id
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    newStorage
  ]
}
