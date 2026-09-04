@description('Name of the existing Azure AI Services (Foundry) account that hosts the agent.')
param accountName string

@description('Base RAI policy from which content filters are inherited.')
param basePolicyName string = 'Microsoft.DefaultV2'

@description('Name of the egress policy attached to the deployed agent.')
param policyName string = 'allow-httpbin'

resource account 'Microsoft.CognitiveServices/accounts@2026-05-15-preview' existing = {
  name: accountName
}

resource egressPolicy 'Microsoft.CognitiveServices/accounts/raiPolicies@2026-05-15-preview' = {
  parent: account
  name: policyName
  properties: {
    basePolicyName: basePolicyName
    contentFilters: []
    egressPolicy: {
      mode: 'Enforced'
      defaultAction: 'Deny'
      rules: [
        {
          name: 'allow-httpbin'
          ruleType: 'Fqdn'
          match: {
            host: 'httpbin.org'
          }
          action: {
            actionType: 'Allow'
          }
        }
      ]
    }
  }
}

@description('Full ARM resource ID of the policy attached to the agent.')
output RAI_POLICY_ID string = egressPolicy.id
