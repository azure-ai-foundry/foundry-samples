/*
Connections enable your AI applications to access tools and objects managed elsewhere in or outside of Azure.

This example demonstrates how to add an Azure Application Insights connection.

Only one application insights can be set on a project at a time.
*/
param aiFoundryName string = '<your-foundry-name>'
param connectedResourceName string = 'appi${aiFoundryName}'
param location string = 'westus'

// Whether to create a new Application Insights resource
@allowed([
  'new'
  'existing'
])
param newOrExisting string = 'new'
 
// Refers your existing Azure AI Foundry resource
resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: aiFoundryName
  scope: resourceGroup()
}

// Log Analytics workspace backing the new Application Insights instance (workspace-based)
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (newOrExisting == 'new') {
  name: 'law-${connectedResourceName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Conditionally refers your existing Application Insights resource
resource existingAppInsights 'Microsoft.Insights/components@2020-02-02' existing = if (newOrExisting == 'existing') {
  name: connectedResourceName
}

// Conditionally creates a new Application Insights resource (workspace-based)
resource newAppInsights 'Microsoft.Insights/components@2020-02-02' = if (newOrExisting == 'new') {
  name: connectedResourceName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// Creates the Azure Foundry connection to your Azure App Insights resource
resource connection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  name: '${aiFoundryName}-appinsights'
  parent: aiFoundry
  properties: {
    category: 'AppInsights'
    target: ((newOrExisting == 'new') ? newAppInsights.id : existingAppInsights.id)
    authType: 'ApiKey'
    isSharedToAll: true
    credentials: {
      key: ((newOrExisting == 'new') ? newAppInsights.properties.ConnectionString : existingAppInsights.properties.ConnectionString)
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: ((newOrExisting == 'new') ? newAppInsights.id : existingAppInsights.id)
    }
  }
}
