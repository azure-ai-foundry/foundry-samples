// Creates Application Insights (workspace-based) and wires it to an AI Foundry account connection
// so that Tracing is available for all projects under that account.

@description('Azure region of the deployment')
param location string

@description('Name of the Application Insights resource')
param appInsightsName string

@description('Name of the Log Analytics workspace that backs Application Insights')
param logAnalyticsWorkspaceName string

@description('Name of the existing AI Foundry account to attach the connection to')
param accountName string

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

resource appInsightsConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  name: '${accountName}-appinsights'
  parent: account
  properties: {
    category: 'AppInsights'
    target: appInsights.id
    authType: 'ApiKey'
    isSharedToAll: true
    credentials: {
      key: appInsights.properties.ConnectionString
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: appInsights.id
    }
  }
}

output appInsightsName string = appInsights.name
output appInsightsId string = appInsights.id
