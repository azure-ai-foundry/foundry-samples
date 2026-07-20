/*
Connections enable your AI applications to access tools and objects managed elsewhere in or outside of Azure.

This example demonstrates how to add an Azure Application Insights connection.

It creates the connection on both the Microsoft Foundry account and the project, and assigns the
project's system-assigned managed identity read access on the Application Insights component so
evaluation can read the agent traces (Privileged Monitoring Data Reader is required to read GenAI content).

Only one application insights can be set on a project at a time.
*/
param aiFoundryName string = '<your-account-name>'

@description('Name of the project (sub-resource of the AI Foundry account) to create the connection on.')
param aiProjectName string = '<your-project-name>'

param connectedResourceName string = 'appi${aiFoundryName}'
param location string = 'westus'

// Share connection with all users
param isSharedToAll bool = true

// Whether to create a new Azure Application Insights resource
@allowed([
  'new'
  'existing'
])
param newOrExisting string = 'new'

// Log Analytics Reader + Privileged Monitoring Data Reader (latter required to read GenAI content)
param roleDefinitionGuids array = [
  '73c42c96-874c-492b-b04d-ab87d138a893'
  'dbc9c667-e97f-4491-aee6-90b9cf960190'
]

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

// Conditionally creates a new Azure Application Insights resource
resource newAppInsights 'Microsoft.Insights/components@2020-02-02' = if (newOrExisting == 'new') {
  name: connectedResourceName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

// Normalized reference to the target Application Insights (works for both new and existing)
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: connectedResourceName
}

// Creates the Azure Foundry account-level connection to your Application Insights resource
resource accountConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  name: '${aiFoundryName}-appinsights'
  parent: aiFoundry
  properties: {
    category: 'AppInsights'
    target: appInsights.id
    authType: 'ApiKey'
    isSharedToAll: isSharedToAll
    credentials: {
      key: appInsights.properties.ConnectionString
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: appInsights.id
    }
  }
  dependsOn: [
    newAppInsights
  ]
}

// Creates the project-level connection to your Application Insights resource
resource projectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = {
  name: connectedResourceName
  parent: aiProject
  properties: {
    category: 'AppInsights'
    target: appInsights.id
    authType: 'ApiKey'
    isSharedToAll: isSharedToAll
    credentials: {
      key: appInsights.properties.ConnectionString
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: appInsights.id
    }
  }
  dependsOn: [
    newAppInsights
  ]
}

resource appInsightsReaderRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleGuid in roleDefinitionGuids: {
  scope: appInsights
  name: guid(aiProject.id, roleGuid, appInsights.id)
  properties: {
    principalId: aiProject.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleGuid)
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    newAppInsights
  ]
}]
