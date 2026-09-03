using './main.bicep'

// ---------------------------------------------------------------------------
// Region — every resource in this sample deploys here. Must be an AIGateway
// preview region (eastus2 / swedencentral). Named `region` (not `location`) so
// generic tooling that rewrites `location` cannot relocate the AIGateway.
// ---------------------------------------------------------------------------
param region = 'swedencentral'

// ---------------------------------------------------------------------------
// Foundation (network-secured standard agent). Defaults create everything new;
// set the optional existing* resource IDs to reuse resources you already have.
// ---------------------------------------------------------------------------
param aiServices = 'aiservices'
param firstProjectName = 'project'
param vnetName = 'agent-vnet-test'
param agentSubnetName = 'agent-subnet'
param peSubnetName = 'pe-subnet'

// Optional reuse (leave empty to create new):
// param existingVnetResourceId = ''
// param aiSearchResourceId = ''
// param azureStorageAccountResourceId = ''
// param azureCosmosDBAccountResourceId = ''

// ---------------------------------------------------------------------------
// Model — deployed on both the consumer account and the private hub. Capacity 1
// keeps the sample inside default quota; raise it for real throughput.
// ---------------------------------------------------------------------------
param modelName = 'gpt-5.4'
param modelFormat = 'OpenAI'
param modelVersion = '2026-03-05'
param modelSkuName = 'GlobalStandard'
param modelCapacity = 1

// ---------------------------------------------------------------------------
// AI Gateway tier layer
// ---------------------------------------------------------------------------
// Outbound integration subnet (a free /27 inside the VNet address space):
param apimOutboundSubnetName = 'apim-outbound'
param apimOutboundSubnetPrefix = '192.168.2.0/27'

// Private hub Foundry account (model backend behind the gateway):
param hubAccountName = 'aigwhub'

// AI Gateway tier instance:
param gatewayName = ''
param publisherEmail = 'noreply@example.com'
param publisherName = 'AI Gateway tier hub (private)'
param tokensPerMinute = 100

// Connection on the project:
param connectionName = 'ai-gateway'
param authHeaderName = 'api-key'
param isSharedToAll = true
