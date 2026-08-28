@description('Name of the existing Microsoft Foundry account.')
param accountName string

resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
}

// The preview API supports UserEntraToken and managed connector properties
// that are not yet represented in the Bicep resource type.
resource workiqMailConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  parent: aiFoundry
  name: 'workiq-mail-conn'
  properties: any({
    category: 'RemoteTool'
    target: 'https://agent365.svc.cloud.microsoft/agents/servers/mcp_MailTools'
    authType: 'UserEntraToken'
    audience: 'ea9ffc3e-8a23-4a7d-836d-234d7c7565c1'
    isSharedToAll: true
  })
}

resource workiqCalendarConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  parent: aiFoundry
  name: 'workiq-calendar-conn'
  properties: any({
    category: 'RemoteTool'
    target: 'https://agent365.svc.cloud.microsoft/agents/servers/mcp_CalendarTools'
    authType: 'UserEntraToken'
    audience: 'ea9ffc3e-8a23-4a7d-836d-234d7c7565c1'
    isSharedToAll: true
  })
}

resource githubOAuthConnection 'Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview' = {
  parent: aiFoundry
  name: 'github-oauth-conn'
  properties: any({
    category: 'RemoteTool'
    target: 'https://api.githubcopilot.com/mcp'
    authType: 'OAuth2'
    connectorName: 'foundrygithubmcp'
    isSharedToAll: true
    credentials: {
      type: 'OAuth2'
      clientId: 'managed'
      clientSecret: 'managed'
    }
  })
}
