/*
Connections enable your AI applications to access tools and objects managed
elsewhere in or outside of Azure.

This example demonstrates how to add a RemoteTool connection for MCP servers
and external tool APIs.

RemoteTool connections support multiple authentication types:
- CustomKeys: API key authentication
- OAuth2: OAuth2 flow for third-party services (GitHub, etc.)
- UserEntraToken: Microsoft Entra token (on-behalf-of flow)
- ProjectManagedIdentity: Azure managed identity (project scope)
- AgenticIdentityToken: Azure managed identity (agentic identity token)

Run command:
az deployment group create \
  --name RemoteToolConnection \
  --resource-group {RESOURCE-GROUP-NAME} \
  --template-file connection-remote-tool.bicep \
  --parameters aiFoundryName={FOUNDRY-RESOURCE-NAME} \
               projectName={PROJECT-NAME} \
               connectionName={CONNECTION-NAME} \
               targetUrl={MCP-SERVER-URL}

Example with CustomKeys auth:
az deployment group create --name RemoteToolConnection --resource-group myRG \
  --template-file connection-remote-tool.bicep \
  --parameters aiFoundryName=myFoundry projectName=myProject \
               connectionName=my-mcp-server targetUrl=https://mcp.example.com \
               authType=CustomKeys apiKey=sk-xxx

Example with OAuth2 auth:
az deployment group create --name RemoteToolConnection --resource-group myRG \
  --template-file connection-remote-tool.bicep \
  --parameters aiFoundryName=myFoundry projectName=myProject \
               connectionName=github-mcp targetUrl=https://api.githubcopilot.com/mcp \
               authType=OAuth2 clientId=xxx clientSecret=xxx \
               tokenUrl=https://github.com/login/oauth/access_token \
               authorizationUrl=https://github.com/login/oauth/authorize

Example with ProjectManagedIdentity:
az deployment group create --name RemoteToolConnection --resource-group myRG \
  --template-file connection-remote-tool.bicep \
  --parameters aiFoundryName=myFoundry projectName=myProject \
               connectionName=azure-mcp targetUrl=https://azure-mcp.example.com \
               authType=ProjectManagedIdentity audience=https://cognitiveservices.azure.com
*/

// Required parameters
param aiFoundryName string
param projectName string
param connectionName string
param targetUrl string

// Authentication type
@allowed([
  'CustomKeys'
  'OAuth2'
  'UserEntraToken'
  'ProjectManagedIdentity'
  'AgenticIdentityToken'
])
param authType string = 'CustomKeys'

// CustomKeys parameters
@secure()
param apiKey string = ''
param apiKeyName string = 'api-key'

// OAuth2 parameters
@secure()
param clientId string = ''
@secure()
param clientSecret string = ''
param tokenUrl string = ''
param authorizationUrl string = ''
param refreshUrl string = ''
param scopes array = []

// UserEntraToken / ProjectManagedIdentity parameters
param audience string = ''

// Connection settings
param isSharedToAll bool = true
param sharedUserList array = []

// Refers to your existing Azure AI Foundry resource
resource aiFoundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: aiFoundryName
  scope: resourceGroup()
}

// Refers to your existing project
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' existing = {
  name: projectName
  parent: aiFoundry
}

// Creates the RemoteTool connection with CustomKeys authentication
resource connectionCustomKeys 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = if (authType == 'CustomKeys') {
  name: connectionName
  parent: project
  properties: {
    category: 'RemoteTool'
    target: targetUrl
    authType: 'CustomKeys'
    isSharedToAll: isSharedToAll
    sharedUserList: sharedUserList
    credentials: {
      keys: {
        '${apiKeyName}': apiKey
      }
    }
    metadata: {
      ApiType: 'Azure'
    }
  }
}

// Creates the RemoteTool connection with OAuth2 authentication
resource connectionOAuth2 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = if (authType == 'OAuth2') {
  name: connectionName
  parent: project
  properties: {
    category: 'RemoteTool'
    target: targetUrl
    authType: 'OAuth2'
    isSharedToAll: isSharedToAll
    sharedUserList: sharedUserList
    tokenUrl: tokenUrl
    authorizationUrl: authorizationUrl
    refreshUrl: refreshUrl
    scopes: scopes
    credentials: {
      clientId: clientId
      clientSecret: clientSecret
    }
    metadata: {
      ApiType: 'Azure'
    }
  }
}

// Creates the RemoteTool connection with UserEntraToken (on-behalf-of) authentication
resource connectionUserEntraToken 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = if (authType == 'UserEntraToken') {
  name: connectionName
  parent: project
  properties: {
    category: 'RemoteTool'
    target: targetUrl
    authType: 'UserEntraToken'
    isSharedToAll: isSharedToAll
    sharedUserList: sharedUserList
    audience: audience
    useCustomConnector: false
    metadata: {
      ApiType: 'Azure'
    }
  }
}

// Creates the RemoteTool connection with ProjectManagedIdentity authentication
resource connectionManagedIdentity 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = if (authType == 'ProjectManagedIdentity') {
  name: connectionName
  parent: project
  properties: {
    category: 'RemoteTool'
    target: targetUrl
    authType: 'ProjectManagedIdentity'
    isSharedToAll: isSharedToAll
    sharedUserList: sharedUserList
    audience: audience
    credentials: {}
    metadata: {
      ApiType: 'Azure'
    }
  }
}

// Creates the RemoteTool connection with AgenticIdentityToken authentication
resource connectionAgenticIdentity 'Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview' = if (authType == 'AgenticIdentityToken') {
  name: connectionName
  parent: project
  properties: {
    category: 'RemoteTool'
    target: targetUrl
    authType: 'AgenticIdentityToken'
    isSharedToAll: isSharedToAll
    sharedUserList: sharedUserList
    audience: audience
    credentials: {}
    metadata: {
      ApiType: 'Azure'
    }
  }
}

// Output the connection ID for use in agent tools
output connectionId string = authType == 'CustomKeys' ? connectionCustomKeys.id : (authType == 'OAuth2' ? connectionOAuth2.id : (authType == 'UserEntraToken' ? connectionUserEntraToken.id : (authType == 'ProjectManagedIdentity' ? connectionManagedIdentity.id : connectionAgenticIdentity.id)))
output connectionName string = connectionName
