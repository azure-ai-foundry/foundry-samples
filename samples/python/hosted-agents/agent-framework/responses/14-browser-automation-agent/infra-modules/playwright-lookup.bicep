targetScope = 'resourceGroup'

@description('Name of the existing Playwright workspace')
param playwrightWorkspaceName string

resource playwrightWorkspace 'Microsoft.LoadTestService/playwrightWorkspaces@2025-09-01' existing = {
  name: playwrightWorkspaceName
}

output dataplaneUri string = playwrightWorkspace.properties.dataplaneUri
