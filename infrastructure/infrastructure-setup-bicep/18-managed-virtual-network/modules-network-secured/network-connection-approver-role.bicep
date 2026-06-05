@description('The principal ID of the AI Services account managed identity')
param aiAccountPrincipalId string

@description('The AI Services account resource ID (used for deterministic GUID generation)')
param aiAccountResourceId string

// Azure AI Enterprise Network Connection Approver role definition ID
var networkConnectionApproverRoleId = 'b556d68e-0be0-4f35-a333-ad7ee1ce17ea'

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiAccountResourceId, networkConnectionApproverRoleId, resourceGroup().id)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', networkConnectionApproverRoleId)
    principalId: aiAccountPrincipalId
    principalType: 'ServicePrincipal'
  }
}
