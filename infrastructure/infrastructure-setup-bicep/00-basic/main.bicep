param aiFoundryName string = 'foundry-name'
param aiProjectName string = '${aiFoundryName}-proj'
param location string = 'eastus2'
param appInsightsName string = 'appi-${aiFoundryName}'

/*
  An AI Foundry resources is a variant of a CognitiveServices/account resource type
*/ 
resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: aiFoundryName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  properties: {
    // required to work in AI Foundry
    allowProjectManagement: true

    // Defines developer API endpoint subdomain
    customSubDomainName: aiFoundryName

    disableLocalAuth: false
  }
}

/*
  Developer APIs are exposed via a project, which groups in- and outputs that relate to one use case, including files.
  Its advisable to create one project right away, so development teams can directly get started.
  Projects may be granted individual RBAC permissions and identities on top of what account provides.
*/ 
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  name: aiProjectName
  parent: aiFoundry
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

/*
  Optionally deploy a model to use in playground, agents and other tools.
*/
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01'= {
  parent: aiFoundry
  name: 'gpt-4.1-mini'
  sku : {
    capacity: 1
    name: 'GlobalStandard'
  }
  properties: {
    model:{
      name: 'gpt-4.1-mini'
      format: 'OpenAI'
      version: '2025-04-14'
    }
  }
}

/*
  Optionally deploy a model to use in playground, agents and other tools.
*/
// resource modelDeployment2 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01'= {
//   parent: aiFoundry
//   name: 'FLUX.1-Kontext-pro'
//   sku : {
//     capacity: 1
//     name: 'GlobalStandard'
//   }
//   properties: {
//     model: {
//       name: 'FLUX.1-Kontext-pro'
//       format: 'Black Forest Labs'
//       version: '1'
//     }
//   }
// }

/*
  A Log Analytics workspace backing Application Insights (workspace-based Application Insights is recommended).
*/
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-${aiFoundryName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

/*
  Application Insights enables Tracing in AI Foundry so you can monitor and debug agent runs,
  evaluations, and other AI workloads end-to-end.
*/
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

/*
  Connect Application Insights to the AI Foundry account so all projects can use it for tracing.
  The connection string is shared with AI Foundry so it can route telemetry to this resource.
*/
resource appInsightsConnection 'Microsoft.CognitiveServices/accounts/connections@2025-06-01' = {
  name: '${aiFoundryName}-appinsights'
  parent: aiFoundry
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
