@description('Location for all resources')
param location string

@description('Unique suffix for resource naming')
param suffix string

@description('Name of the VNet to link DNS zones to')
param vnetName string

@description('Resource group of the VNet')
param vnetResourceGroupName string

@description('Subscription ID of the VNet')
param vnetSubscriptionId string

@description('Name of the subnet for private endpoints')
param peSubnetName string

// Reference existing VNet and subnet
resource vnet 'Microsoft.Network/virtualNetworks@2023-04-01' existing = {
  name: vnetName
  scope: resourceGroup(vnetSubscriptionId, vnetResourceGroupName)
}

resource peSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-04-01' existing = {
  parent: vnet
  name: peSubnetName
}

// Create Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-analytics-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Create Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'app-insights-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// Create Azure Monitor Private Link Scope
resource ampls 'microsoft.insights/privateLinkScopes@2021-07-01-preview' = {
  name: 'ampls-${suffix}'
  location: 'global'
  properties: {
    accessModeSettings: {
      ingestionAccessMode: 'PrivateOnly'
      queryAccessMode: 'PrivateOnly'
    }
  }
}

// Link Application Insights to AMPLS
resource appInsightsLink 'Microsoft.Insights/privateLinkScopes/scopedResources@2021-07-01-preview' = {
  parent: ampls
  name: 'appinsights-link'
  properties: {
    linkedResourceId: appInsights.id
  }
}

// Link Log Analytics Workspace to AMPLS
resource logAnalyticsLink 'Microsoft.Insights/privateLinkScopes/scopedResources@2021-07-01-preview' = {
  parent: ampls
  name: 'loganalytics-link'
  properties: {
    linkedResourceId: logAnalyticsWorkspace.id
  }
}

// Private DNS Zones required for AMPLS
var amplsDnsZones = [
  'privatelink.monitor.azure.com'
  'privatelink.oms.opinsights.azure.com'
  'privatelink.ods.opinsights.azure.com'
  'privatelink.agentsvc.azure-automation.net'
]

resource dnsZones 'Microsoft.Network/privateDnsZones@2020-06-01' = [
  for zone in amplsDnsZones: {
    name: zone
    location: 'global'
  }
]

resource dnsZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = [
  for (zone, i) in amplsDnsZones: {
    parent: dnsZones[i]
    name: '${replace(zone, '.', '-')}-link'
    location: 'global'
    properties: {
      virtualNetwork: {
        id: vnet.id
      }
      registrationEnabled: false
    }
  }
]

// Create Private Endpoint for AMPLS
resource amplsPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'ampls-pe-${suffix}'
  location: location
  properties: {
    subnet: {
      id: peSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'ampls-connection'
        properties: {
          privateLinkServiceId: ampls.id
          groupIds: [
            'azuremonitor'
          ]
        }
      }
    ]
  }
  dependsOn: [
    appInsightsLink
    logAnalyticsLink
  ]
}

// DNS Zone Group for automatic DNS record registration
resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-04-01' = {
  parent: amplsPrivateEndpoint
  name: 'ampls-dns-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'monitor'
        properties: {
          privateDnsZoneId: dnsZones[0].id
        }
      }
      {
        name: 'oms'
        properties: {
          privateDnsZoneId: dnsZones[1].id
        }
      }
      {
        name: 'ods'
        properties: {
          privateDnsZoneId: dnsZones[2].id
        }
      }
      {
        name: 'agentsvc'
        properties: {
          privateDnsZoneId: dnsZones[3].id
        }
      }
    ]
  }
}

output amplsResourceId string = ampls.id
output appInsightsName string = appInsights.name
output appInsightsResourceId string = appInsights.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
