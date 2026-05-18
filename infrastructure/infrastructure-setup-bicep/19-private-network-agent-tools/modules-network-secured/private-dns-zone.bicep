/*
Private DNS Zone (per-zone) module
Creates a private DNS zone (or references an existing one) and links it to the supplied VNet.
Used by `private-endpoint-and-dns.bicep` in a `for` loop over `existingDnsZones`.

When `enabled` is false the module is a no-op (used to skip optional zones such as Fabric).
*/

@description('Fully-qualified DNS zone name (e.g. privatelink.openai.azure.com).')
param zoneName string

@description('Resource group of an existing zone. Empty means create the zone in this RG.')
param existingResourceGroup string = ''

@description('Subscription ID of an existing zone. Empty defaults to the current subscription. Only used when existingResourceGroup is non-empty.')
param existingSubscriptionId string = ''

@description('ARM ID of the VNet to link the zone to.')
param vnetId string

@description('Suffix used to make the vnet-link name unique.')
param suffix string

@description('Disable the entire module (zone, link, output). Used to skip optional zones.')
param enabled bool = true

var shouldCreate = enabled && empty(existingResourceGroup)
var shouldReference = enabled && !empty(existingResourceGroup)
var effectiveExistingSubscriptionId = empty(existingSubscriptionId) ? subscription().subscriptionId : existingSubscriptionId

resource newZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (shouldCreate) {
  name: zoneName
  location: 'global'
}

resource existingZone 'Microsoft.Network/privateDnsZones@2020-06-01' existing = if (shouldReference) {
  name: zoneName
  scope: resourceGroup(effectiveExistingSubscriptionId, existingResourceGroup)
}

// Only link when we own the zone. If the user supplied an existing zone in another RG,
// link management is their responsibility (and we may not have rights to write into that RG).
resource link 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (shouldCreate) {
  parent: newZone
  name: '${replace(zoneName, '.', '-')}-${suffix}-link'
  location: 'global'
  properties: {
    virtualNetwork: { id: vnetId }
    registrationEnabled: false
  }
}

output zoneId string = enabled ? (shouldCreate ? newZone.id : existingZone.id) : ''
output zoneName string = zoneName
