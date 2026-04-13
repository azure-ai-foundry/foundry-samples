// Azure Function App with VNet Integration and Private Endpoint
//
// Deploys a minimal Python Azure Function with VNet networking:
//   - VNet Integration for outbound traffic (function can reach private resources)
//   - Private Endpoint for inbound access from VNet callers (optional)
//   - Private DNS zone for privatelink.azurewebsites.net
//   - Storage Private Endpoints (required for Functions runtime)
//
// IMPORTANT: publicNetworkAccess is set to 'Enabled' because the Foundry DataProxy
// resolves DNS at the infrastructure level, not through VNet private DNS zones.
// Setting it to 'Disabled' causes 403 Ip Forbidden errors when the Function is used
// as an OpenAPI tool. Use App Service access restrictions for inbound IP filtering.
//
// Usage:
//   az deployment group create \
//     --resource-group $RESOURCE_GROUP \
//     --template-file deploy-function.bicep \
//     --parameters vnetName=<vnet> privateEndpointSubnetName=<subnet> location=<region>

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Name of the existing VNet')
param vnetName string

@description('Name of an existing or new subnet for the Function App VNet Integration')
param integrationSubnetName string = 'func-integration-subnet'

@description('Address prefix for the integration subnet (if creating new)')
param integrationSubnetPrefix string = '192.168.5.0/24'

@description('Name of the subnet where Private Endpoints will be created')
param privateEndpointSubnetName string

@description('Base name for the Function App resources')
param baseName string = 'functest${uniqueString(resourceGroup().id)}'

// --- Existing VNet ---
resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' existing = {
  name: vnetName
}

resource peSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' existing = {
  parent: vnet
  name: privateEndpointSubnetName
}

// --- Integration Subnet (delegated to Microsoft.Web/serverFarms) ---
resource integrationSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-01-01' = {
  parent: vnet
  name: integrationSubnetName
  properties: {
    addressPrefix: integrationSubnetPrefix
    delegations: [
      {
        name: 'Microsoft.Web.serverFarms'
        properties: {
          serviceName: 'Microsoft.Web/serverFarms'
        }
      }
    ]
  }
}

// --- Storage Account (required by Functions runtime) ---
// NOTE: Storage must be accessible during Function App creation (file share).
// Deploy with defaultAction: 'Allow' first, then restrict after Function App is created.
// After restricting, restart the Function App to avoid 503 errors.
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: '${take(baseName, 20)}stor'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// --- Storage Private Endpoints (Blob + Queue + File — required for Functions) ---
// The Functions runtime needs all three: Blob for triggers/bindings,
// Queue for internal messaging, File for the content share (WEBSITE_CONTENTSHARE).
resource storageBlobPe 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: '${baseName}-blob-pe'
  location: location
  properties: {
    subnet: { id: peSubnet.id }
    privateLinkServiceConnections: [
      {
        name: 'blob'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: ['blob']
        }
      }
    ]
  }
}

resource storageQueuePe 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: '${baseName}-queue-pe'
  location: location
  properties: {
    subnet: { id: peSubnet.id }
    privateLinkServiceConnections: [
      {
        name: 'queue'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: ['queue']
        }
      }
    ]
  }
}

resource storageFilePe 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: '${baseName}-file-pe'
  location: location
  properties: {
    subnet: { id: peSubnet.id }
    privateLinkServiceConnections: [
      {
        name: 'file'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: ['file']
        }
      }
    ]
  }
}

// --- App Service Plan (Elastic Premium for VNet features) ---
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${baseName}-plan'
  location: location
  sku: {
    name: 'EP1'
    tier: 'ElasticPremium'
  }
  kind: 'elastic'
  properties: {
    reserved: true // Linux
  }
}

// --- Function App ---
// publicNetworkAccess must be 'Enabled' for DataProxy compatibility.
// The DataProxy resolves DNS at the Foundry infrastructure level, not through
// VNet private DNS zones. 'Disabled' causes 403 Ip Forbidden errors.
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${baseName}-func'
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: appServicePlan.id
    publicNetworkAccess: 'Enabled'
    virtualNetworkSubnetId: integrationSubnet.id
    siteConfig: {
      pythonVersion: '3.11'
      linuxFxVersion: 'PYTHON|3.11'
      appSettings: [
        { name: 'FUNCTIONS_WORKER_RUNTIME'; value: 'python' }
        { name: 'FUNCTIONS_EXTENSION_VERSION'; value: '~4' }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        { name: 'WEBSITE_CONTENTOVERVNET'; value: '1' }
        { name: 'WEBSITE_VNET_ROUTE_ALL'; value: '1' }
      ]
    }
  }
}

// --- Function App Private Endpoint ---
resource functionPe 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: '${baseName}-func-pe'
  location: location
  properties: {
    subnet: { id: peSubnet.id }
    privateLinkServiceConnections: [
      {
        name: 'sites'
        properties: {
          privateLinkServiceId: functionApp.id
          groupIds: ['sites']
        }
      }
    ]
  }
}

// --- Private DNS Zone for Function App ---
resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.azurewebsites.net'
  location: 'global'
}

resource dnsVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: '${vnetName}-link'
  location: 'global'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}

resource functionDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: functionPe
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-azurewebsites-net'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

// --- Private DNS Zones for Storage ---
resource storageBlobDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.blob.${environment().suffixes.storage}'
  location: 'global'
}

resource storageBlobDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: storageBlobDns
  name: '${vnetName}-blob-link'
  location: 'global'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}

resource storageBlobDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: storageBlobPe
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-blob'
        properties: {
          privateDnsZoneId: storageBlobDns.id
        }
      }
    ]
  }
}

resource storageQueueDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.queue.${environment().suffixes.storage}'
  location: 'global'
}

resource storageQueueDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: storageQueueDns
  name: '${vnetName}-queue-link'
  location: 'global'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}

resource storageQueueDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: storageQueuePe
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-queue'
        properties: {
          privateDnsZoneId: storageQueueDns.id
        }
      }
    ]
  }
}

// --- File DNS Zone ---
resource storageFileDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.file.${environment().suffixes.storage}'
  location: 'global'
}

resource storageFileDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: storageFileDns
  name: '${vnetName}-file-link'
  location: 'global'
  properties: {
    virtualNetwork: { id: vnet.id }
    registrationEnabled: false
  }
}

resource storageFileDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: storageFilePe
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-file'
        properties: {
          privateDnsZoneId: storageFileDns.id
        }
      }
    ]
  }
}

// --- Outputs ---
output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output functionPrivateEndpointId string = functionPe.id
output functionAppResourceId string = functionApp.id
