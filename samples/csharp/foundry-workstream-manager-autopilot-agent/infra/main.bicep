targetScope = 'resourceGroup'

// =================================================================================================
// Main parameters
// =================================================================================================

@minLength(1)
@maxLength(64)
@description('Name of the application. Used to ensure resource names are unique.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

// =================================================================================================
// Project module parameters
// =================================================================================================

@description('Name of the Cognitive Services account')
param accountName string = '${environmentName}acct'

@description('Name of the Cognitive Services project')
param projectName string = '${environmentName}proj'

@description('Name of the Container Registry')
param containerRegistryName string = '${environmentName}acr'

@description('SKU of Cognitive Services account')
param cognitiveServicesSku string = 'S0'

@description('SKU of Container Registry')
@allowed(['Basic', 'Standard', 'Premium'])
param containerRegistrySku string = 'Basic'

@description('Name of the model to deploy')
param modelName string = 'gpt-5-chat'

@description('Version of the model to deploy')
param modelVersion string = '2025-10-03'

@description('Enable monitoring via Application Insights and Log Analytics')
param enableMonitoring bool = true

@description('Name of the Log Analytics workspace')
param logAnalyticsName string = '${environmentName}-logs'

@description('Name of the Application Insights instance')
param applicationInsightsName string = '${environmentName}-appi'

param agentName string = '${environmentName}-agent'

param maibName string = '${environmentName}-maib'

// =================================================================================================
// Bot Service module parameters
// =================================================================================================

@description('Name of the Bot Service 1')
param botName string = '${environmentName}-bot'

@description('Display name of the bot')
param botDisplayName string = '${environmentName} Bot'

@description('SKU of the Bot Service')
param botServiceSku string = 'F0'

// =================================================================================================
// Azure Table Storage parameters
// =================================================================================================

@description('Storage account used for agent table data (allowlist and work items)')
param storageAccountName string = take(toLower(replace('${environmentName}storage', '-', '')), 24)

@description('Table name used for direct-message allowlist data')
param directMessageAllowListTableName string = 'digitalworkerallowlist'

@description('Table name used for work items data')
param workItemsTableName string = 'workitems'

// =================================================================================================
// Common parameters
// =================================================================================================

@description('Tags to apply to all resources')
param tags object = {}

// =================================================================================================
// Module deployments
// =================================================================================================

// 1. Deploy the project module (Cognitive Services account, project, and Container Registry)
module project 'modules/project.bicep' = {
  name: 'project1-deployment'
  params: {
    accountName: accountName
    projectName: projectName
    containerRegistryName: containerRegistryName
    location: location
    tags: tags
    cognitiveServicesSku: cognitiveServicesSku
    containerRegistrySku: containerRegistrySku
    modelName: modelName
    modelVersion: modelVersion
    enableMonitoring: enableMonitoring
    logAnalyticsName: logAnalyticsName
    applicationInsightsName: applicationInsightsName
  }
}

// 2. Create deployment script UMI and grant roles on RG.
module deploymentScriptUmi 'modules/deployment-script-umi.bicep' = {
  name: 'deployment-script-umi'
  dependsOn: [
    project
  ]
}

// 3. Create managed agent identity blueprint using a deployment script as that is a dataplane operation.
module deploymentScriptAgent 'modules/maib-creation-script.bicep' = {
  name: 'maib-creation-script'
  params: {
    uamiResourceId: deploymentScriptUmi.outputs.uamiResourceId
    azureAIProjectEndpoint: project.outputs.foundryProjectEndpoint
    maibName: maibName
  }
  dependsOn: [
    deploymentScriptUmi
  ]
}


// 4. Deploy the bot service module
module botService 'modules/botservice.bicep' = {
  name: 'botservice-deployment'
  params: {
    botName: botName
    displayName: botDisplayName
    msaAppId: deploymentScriptAgent.outputs.blueprintClientId
    endpoint: 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}/agents/${agentName}/endpoint/protocols/activityProtocol?api-version=2025-05-15-preview'
    botServiceSku: botServiceSku
  }
  dependsOn: [
    deploymentScriptAgent
  ]
}

// 5. Deploy Azure Table Storage for agent data (allowlist + work items).
module tables 'modules/tables.bicep' = {
  name: 'tables-deployment'
  params: {
    storageAccountName: storageAccountName
    tableNames: [
      directMessageAllowListTableName
      workItemsTableName
    ]
    location: location
    tags: tags
  }
}

// =================================================================================================
// Outputs - These become environment variables in post-provision.sh
// =================================================================================================

@description('ACR login server endpoint')
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = project.outputs.acrloginServer

output AZURE_AI_PROJECT_ENDPOINT string = project.outputs.foundryProjectEndpoint

@description('Agent identity blueprint ID')
output AGENT_IDENTITY_BLUEPRINT_ID string = deploymentScriptAgent.outputs.blueprintClientId

output SUBSCRIPTION_ID string = subscription().subscriptionId

output LOCATION string = location

output ACCOUNT_NAME string = accountName

output PROJECT_NAME string = projectName

output AGENT_NAME string = agentName

output TENANT_ID string = tenant().tenantId

output PROJECT_PRINCIPAL_ID string = project.outputs.foundryProjectPrincipalId

output MAIB_NAME string = maibName

output PROJECT_DEFAULT_INSTANCE_CLIENT_ID string = project.outputs.foundryProjectDefaultInstanceClientId

output DIRECT_MESSAGE_ALLOWLIST_TABLE_SERVICE_URI string = tables.outputs.tableServiceUri

output DIRECT_MESSAGE_ALLOWLIST_TABLE_NAME string = directMessageAllowListTableName

output DIRECT_MESSAGE_ALLOWLIST_STORAGE_ACCOUNT_RESOURCE_ID string = tables.outputs.storageAccountResourceId

output WORK_ITEMS_TABLE_SERVICE_URI string = tables.outputs.tableServiceUri

output WORK_ITEMS_TABLE_NAME string = workItemsTableName

output WORK_ITEMS_STORAGE_ACCOUNT_RESOURCE_ID string = tables.outputs.storageAccountResourceId

@description('Application Insights connection string (empty when monitoring is disabled)')
output APPLICATIONINSIGHTS_CONNECTION_STRING string = project.outputs.applicationInsightsConnectionString

@description('Application Insights resource ID (empty when monitoring is disabled)')
output APPLICATIONINSIGHTS_RESOURCE_ID string = project.outputs.applicationInsightsResourceId
