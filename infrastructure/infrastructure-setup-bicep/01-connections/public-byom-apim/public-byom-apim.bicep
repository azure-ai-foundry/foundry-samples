/*
  ================================================================================
  public-byom-apim.bicep  (non-VNet Bring-Your-Own-Model via APIM AI Gateway)
  --------------------------------------------------------------------------------
  Layer-on-existing sample. Given an EXISTING Microsoft Foundry project and an
  EXISTING backend Foundry account that already hosts your model deployments,
  this template stands up the public (non-network-secured) AI Gateway plumbing:

    * A public StandardV2 Azure API Management service with a system-assigned MI
    * A role assignment granting APIM's MI "Cognitive Services User" on the
      backend Foundry account (so APIM can mint tokens for it)
    * The /inference API on APIM with the full managed-identity + backend-rewrite
      policy chain
    * A Bring-Your-Own-Model (BYOM) connection on the project that surfaces the
      backend deployments as <connectionName>/<deploymentName> in agent code

  This is the public (no VNet / no private endpoint) counterpart of the private
  cross-region BYOM extension under
  16-private-network-standard-agent-apim-setup/extensions/byom-cross-region.

  Use this when your Foundry account, project, and backend model account are all
  reachable over public networking and you want the AI Gateway pattern (central
  observability, throttling, governance, managed-identity auth) in front of your
  model traffic.

  Prerequisites (create these first — e.g. with template 40 or 41):
    * A Foundry account + project (the project MUST have a managed identity).
    * A backend Foundry account with your model deployments, in THIS resource
      group (the role assignment is scoped to the current resource group).

  Reference: https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway
  ================================================================================
*/

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Existing resources
// ---------------------------------------------------------------------------
@description('Resource ID of the EXISTING Microsoft Foundry project the BYOM connection is added to.')
param projectResourceId string = '/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/sample-rg/providers/Microsoft.CognitiveServices/accounts/sample-foundry-account/projects/sample-project'

@description('Name of the EXISTING backend Foundry account (in THIS resource group) that hosts the model deployments.')
param backendAccountName string = 'sample-backend-account'

@description('Region of the backend Foundry account. Emitted on the x-aigw-region trace header.')
param backendRegion string = resourceGroup().location

@description('Application (client) ID of the Foundry project managed identity. APIM uses this to validate inbound tokens.')
param projectMiClientId string = '00000000-0000-0000-0000-000000000000'

@description('Whether to create the role assignment granting APIM\'s managed identity "Cognitive Services User" on the backend account. Requires Microsoft.Authorization/roleAssignments/write (Owner or User Access Administrator) on the backend account. Set to false if you deploy as Contributor and arrange this role assignment out-of-band.')
param assignBackendRole bool = true

// ---------------------------------------------------------------------------
// APIM inputs
// ---------------------------------------------------------------------------
@description('Region for the APIM service. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Globally unique APIM service name. Resolves to <name>.azure-api.net. Leave empty to auto-generate.')
param apimName string = ''

@description('Publisher email required by APIM at create time.')
param publisherEmail string

@description('Publisher organization name required by APIM at create time.')
param publisherName string

// ---------------------------------------------------------------------------
// BYOM connection inputs
// ---------------------------------------------------------------------------
@description('Foundry portal name for the AI Gateway connection. Shows up as <connectionName>/<deploymentName> in agent code.')
param connectionName string = 'ai-gateway'

@description('Inference API version sent to the backend by Foundry SDK calls.')
param inferenceApiVersion string = '2024-10-21'

@description('Model deployments on the backend account to surface through the gateway. Each entry: { name, format, version }.')
param backendModelDeployments array = [
  {
    name: 'gpt-4o'
    format: 'OpenAI'
    version: '2024-11-20'
  }
]

var uniqueSuffix = substring(uniqueString(resourceGroup().id), 0, 4)
var effectiveApimName = empty(apimName) ? 'apim-${uniqueSuffix}-aigw' : apimName

// ===========================================================================
// Public StandardV2 APIM service
// ===========================================================================
module apimService 'modules/apim-service-public.bicep' = {
  name: 'apim-${uniqueSuffix}-deployment'
  params: {
    location: location
    apimName: effectiveApimName
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

// ===========================================================================
// Grant APIM's MI access to the backend Foundry account
// (optional — requires roleAssignments/write on the backend account)
// ===========================================================================
module apimBackendRole 'modules/apim-backend-role-assignment.bicep' = if (assignBackendRole) {
  name: 'apim-backend-role-${uniqueSuffix}-deployment'
  params: {
    apimPrincipalId: apimService.outputs.apimPrincipalId
    backendAccountName: backendAccountName
  }
}

// ===========================================================================
// /inference API on APIM with the full MI + backend-rewrite policy chain
// ===========================================================================
module inferenceApi 'modules/apim-inference-api.bicep' = {
  name: 'inference-api-${uniqueSuffix}-deployment'
  params: {
    apimName: apimService.outputs.apimName
    projectMiClientId: projectMiClientId
    backendAccountName: backendAccountName
    backendRegion: backendRegion
    projectRegion: location
  }
  dependsOn: assignBackendRole ? [apimBackendRole] : []
}

// ===========================================================================
// BYOM model connection on the project, pointing at APIM
// (reuses the canonical 01-connections/apim/connection-apim.bicep module)
// ===========================================================================
module byomConnection '../apim/connection-apim.bicep' = {
  name: 'byom-connection-${uniqueSuffix}-deployment'
  params: {
    projectResourceId: projectResourceId
    apimResourceId: apimService.outputs.apimResourceId
    apiName: inferenceApi.outputs.apiName
    connectionName: connectionName
    authType: 'ProjectManagedIdentity'
    isSharedToAll: true
    deploymentInPath: 'true'
    inferenceAPIVersion: inferenceApiVersion
    staticModels: [for d in backendModelDeployments: {
      name: d.name
      properties: {
        model: {
          name: d.name
          version: d.version
          format: d.format
        }
      }
    }]
  }
}

// ===========================================================================
// Outputs
// ===========================================================================
output apimName string = apimService.outputs.apimName
output apimGatewayUrl string = apimService.outputs.apimGatewayUrl
output inferenceApiName string = inferenceApi.outputs.apiName
output byomConnectionName string = byomConnection.outputs.connectionName
