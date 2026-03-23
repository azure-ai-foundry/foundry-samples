using './main.bicep'

param location = 'eastus'
param aiServices = 'foundy'
param firstProjectName = 'project'
param projectDescription = 'AI Foundry Agent project with BYO VNet and GSA proxy'
param displayName = 'BYO VNet GSA Proxy Agent Project'

// VNet parameters
param vnetName = 'agent-vnet'
param vnetAddressPrefix = '10.0.0.0/16'
param agentSubnetName = 'agent-subnet'
param agentSubnetPrefix = '10.0.0.0/24'
param gsaProxySubnetName = 'gsa-proxy-subnet'
param gsaProxySubnetPrefix = '10.0.1.0/24'

// GSA Proxy parameters
param gsaProxyVmSize = 'Standard_D2s_v3'
param gsaProxyAdminUsername = 'azureuser'
param gsaProxySshPublicKey = readEnvironmentVariable('GSA_PROXY_SSH_PUBLIC_KEY', '')

// Model deployment parameters
param modelName = 'gpt-4.1'
param modelFormat = 'OpenAI'
param modelVersion = '2025-04-14'
param modelSkuName = 'GlobalStandard'
param modelCapacity = 30

// Optional: bring existing resources (leave empty to create new)
param aiSearchResourceId = ''
param azureStorageAccountResourceId = ''
param azureCosmosDBAccountResourceId = ''
