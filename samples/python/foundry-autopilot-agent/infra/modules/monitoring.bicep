metadata description = 'Creates Log Analytics and Application Insights for agent telemetry.'

param location string = resourceGroup().location
param tags object = {}

@description('Name of the Log Analytics workspace')
param logAnalyticsName string

@description('Name of the Application Insights component')
param applicationInsightsName string

@description('Principal ID of the Foundry project managed identity')
param projectPrincipalId string

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    retentionInDays: 30
    features: {
      searchVersion: 1
    }
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

var applicationInsightsRoleDefinitionIds = [
  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb') // Monitoring Metrics Publisher
  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '73c42c96-874c-492b-b04d-ab87d138a893') // Log Analytics Reader
  subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'dbc9c667-e97f-4491-aee6-90b9cf960190') // Privileged Monitoring Data Reader
]

resource applicationInsightsRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleDefinitionId in applicationInsightsRoleDefinitionIds: {
  scope: applicationInsights
  name: guid(applicationInsights.id, projectPrincipalId, roleDefinitionId)
  properties: {
    principalId: projectPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: roleDefinitionId
  }
}]

output connectionString string = applicationInsights.properties.ConnectionString
output id string = applicationInsights.id
