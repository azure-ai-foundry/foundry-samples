using './main.bicep'

// ---------------------------------------------------------------------------
// Project region (Foundry account, project, VNet, APIM all land here)
// ---------------------------------------------------------------------------
param location = 'canadaeast'
param aiServices = 'aiservices'
param firstProjectName = 'project'
param projectDescription = 'Cross-region private BYOM via APIM'
param displayName = 'cross-region BYOM project'

// Project-region model (covers local fallbacks; backend hosts the heavyweight ones)
param projectModelName = 'gpt-4o'
param projectModelFormat = 'OpenAI'
param projectModelVersion = '2024-11-20'
param projectModelSkuName = 'GlobalStandard'
param projectModelCapacity = 30

// ---------------------------------------------------------------------------
// VNet
// ---------------------------------------------------------------------------
param vnetName = 'agent-vnet-test'
param vnetAddressPrefix = '192.168.0.0/16'
param agentSubnetName = 'agent-subnet'
param peSubnetName = 'pe-subnet'
param backendPeSubnetName = 'backend-pe'
param backendPeSubnetPrefix = '192.168.3.0/27'
param apimOutboundSubnetName = 'apim-outbound'
param apimOutboundSubnetPrefix = '192.168.2.0/27'

// ---------------------------------------------------------------------------
// APIM (create new — leave apiManagementResourceId empty)
// ---------------------------------------------------------------------------
param apimName = 'apim-aigw-byom'
param publisherEmail = 'platform-eng@contoso.com'
param publisherName = 'Contoso Platform Engineering'
param apiManagementResourceId = ''

// ---------------------------------------------------------------------------
// Backend Foundry account (different region — where the gpt-5* models live)
// ---------------------------------------------------------------------------
param backendLocation = 'japaneast'
param backendAccountName = 'aiservices-backend-jpe'
param backendModelDeployments = [
  {
    name: 'gpt-4o'
    format: 'OpenAI'
    version: '2024-11-20'
    skuName: 'GlobalStandard'
    capacity: 10
  }
  {
    name: 'gpt-5'
    format: 'OpenAI'
    version: '2025-08-07'
    skuName: 'GlobalStandard'
    capacity: 10
  }
  {
    name: 'gpt-5.1'
    format: 'OpenAI'
    version: '2025-11-13'
    skuName: 'GlobalStandard'
    capacity: 10
  }
]

// ---------------------------------------------------------------------------
// BYOM connection
// ---------------------------------------------------------------------------
param connectionName = 'ai-gateway'
param inferenceApiVersion = '2024-10-21'

// Application (client) ID of the Foundry project's User-Assigned or System-
// Assigned managed identity. Required so the APIM validate-azure-ad-token
// policy can verify that incoming tokens were minted by this MI. Look it up
// after the project MI exists (project Identity blade or `az ad sp show`).
param projectMiClientId = '00000000-0000-0000-0000-000000000000'

// ---------------------------------------------------------------------------
// BYO dependencies (leave empty to create new ones)
// ---------------------------------------------------------------------------
param aiSearchResourceId = ''
param azureStorageAccountResourceId = ''
param azureCosmosDBAccountResourceId = ''
