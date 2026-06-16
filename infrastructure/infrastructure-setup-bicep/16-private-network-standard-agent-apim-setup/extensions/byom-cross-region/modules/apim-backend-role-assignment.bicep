/*
  apim-backend-role-assignment.bicep
  ----------------------------------
  Grants APIM's system-assigned MI the Cognitive Services User role on
  the backend Foundry account, so the authentication-managed-identity
  policy in apim-inference-api.bicep can successfully mint Entra tokens
  for the cross-region backend.

  Run scoped to the resource group that holds the backend account.
*/

@description('Object/principal ID of the APIM service MI.')
param apimPrincipalId string

@description('Backend Foundry account name. The role assignment is scoped to this account.')
param backendAccountName string

// Cognitive Services User
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

resource backendAccount 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: backendAccountName
}

resource apimToBackendRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: backendAccount
  name: guid(backendAccount.id, apimPrincipalId, cognitiveServicesUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: apimPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output roleAssignmentId string = apimToBackendRole.id
