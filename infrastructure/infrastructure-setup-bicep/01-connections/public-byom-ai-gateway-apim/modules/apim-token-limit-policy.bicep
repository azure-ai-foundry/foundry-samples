/*
  apim-token-limit-policy.bicep
  -----------------------------
  TC01 step 4 — "add a policy". Attaches an LLM token-limit policy to the
  chat-completions operation of the /inference API.

  Applied at OPERATION scope so the shared API-level policy chain
  (validate-azure-ad-token -> set-backend-service -> authentication-managed-identity)
  in ../../public-byom-apim/modules/apim-inference-api.bicep stays untouched.
  <base /> pulls that chain in first, then llm-token-limit is evaluated.

  llm-token-limit meters real model token usage (prompt + completion) against a
  tokens-per-minute budget shared by callers on the same counter-key, and returns
  HTTP 429 once the budget is exhausted. Token-based limiting is a core AI Gateway
  capability — it governs spend/throughput by actual model consumption rather than
  raw request count.
*/

@description('Name of the APIM service.')
param apimName string

@description('Name (path) of the inference API the operation belongs to.')
param apiName string

@description('Operation to attach the token-limit policy to.')
param operationName string = 'chat-completions'

@description('Tokens-per-minute budget shared across callers on the same counter-key.')
param tokensPerMinute int = 1000

@description('Estimate prompt tokens before calling the backend (true) or meter only actual usage returned by the model (false).')
param estimatePromptTokens bool = true

var estimateStr = estimatePromptTokens ? 'true' : 'false'

// counter-key falls back to caller IP when there is no APIM subscription (this API
// uses managed-identity auth, so subscriptionRequired is false).
var policyXml = '''
<policies>
  <inbound>
    <base />
    <llm-token-limit counter-key="@(context.Subscription?.Id ?? context.Request.IpAddress)" tokens-per-minute="${TPM}" estimate-prompt-tokens="${ESTIMATE}" tokens-consumed-header-name="x-tokens-consumed" remaining-tokens-header-name="x-tokens-remaining" />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
'''

var renderedPolicy = replace(
  replace(policyXml, '\${TPM}', string(tokensPerMinute)),
  '\${ESTIMATE}', estimateStr
)

resource apim 'Microsoft.ApiManagement/service@2024-05-01' existing = {
  name: apimName
}

resource inferenceApi 'Microsoft.ApiManagement/service/apis@2024-05-01' existing = {
  parent: apim
  name: apiName
}

resource operation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' existing = {
  parent: inferenceApi
  name: operationName
}

resource operationPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: operation
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: renderedPolicy
  }
}

output policyId string = operationPolicy.id
