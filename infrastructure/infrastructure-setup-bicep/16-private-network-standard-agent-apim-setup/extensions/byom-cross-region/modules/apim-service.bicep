/*
  apim-service.bicep
  ------------------
  Greenfield Azure API Management instance (StandardV2) with outbound VNet
  integration into the project VNet's apim-outbound subnet, and a system-
  assigned managed identity that APIM uses (via authentication-managed-
  identity in the inference API policy) to mint Entra tokens for the
  backend Foundry account.

  StandardV2 is required for VNet integration on the v2 SKU family.

  If you already have an APIM service you want to put in front of the
  Foundry backend, skip this module and pass the existing APIM resource
  ID to apim-inference-api.bicep instead.
*/

@description('Region for APIM. Should match the project region.')
param location string

@description('Globally unique APIM service name. Resolves to <name>.azure-api.net.')
param apimName string

@description('Resource ID of the apim-outbound subnet in the project VNet.')
param apimOutboundSubnetId string

@description('Publisher email — required by APIM at create time.')
param publisherEmail string

@description('Publisher organization name — required by APIM at create time.')
param publisherName string

@description('StandardV2 capacity units. 1 is enough for a single-region pattern.')
param skuCapacity int = 1

resource apim 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: apimName
  location: location
  sku: {
    name: 'StandardV2'
    capacity: skuCapacity
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
    virtualNetworkType: 'External'
    virtualNetworkConfiguration: {
      subnetResourceId: apimOutboundSubnetId
    }
    publicNetworkAccess: 'Enabled'
  }
}

output apimResourceId string = apim.id
output apimName string = apim.name
output apimGatewayUrl string = apim.properties.gatewayUrl
output apimPrincipalId string = apim.identity.principalId
