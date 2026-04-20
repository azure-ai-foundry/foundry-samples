// Assigns the Azure AI User role to the project managed identity on the AI Services account

@description('Name of the AI Services account')
param accountName string

@description('Principal ID of the AI project')
param projectPrincipalId string

resource account 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: accountName
  scope: resourceGroup()
}

// Azure AI User: 53ca6127-db72-4b80-b1b0-d745d6d5456d
resource azureAIUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '53ca6127-db72-4b80-b1b0-d745d6d5456d'
  scope: resourceGroup()
}

resource azureAIUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(projectPrincipalId, azureAIUserRole.id, account.id)
  properties: {
    principalId: projectPrincipalId
    roleDefinitionId: azureAIUserRole.id
    principalType: 'ServicePrincipal'
  }
}
