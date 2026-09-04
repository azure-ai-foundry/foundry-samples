/*
  apim-service-public.bicep
  -------------------------
  Greenfield public Azure API Management instance (StandardV2) with a
  system-assigned managed identity that APIM uses (via authentication-
  managed-identity in the inference API policy) to mint Entra tokens for
  the backend Foundry account.

  Public counterpart of the VNet-integrated apim-service.bicep under
  16-.../extensions/byom-cross-region/modules. No VNet integration and no
  private endpoints — the APIM gateway is reachable over public networking.

  If you already have an APIM service you want to put in front of the
  Foundry backend, skip this module and pass the existing APIM name to
  apim-inference-api.bicep instead.
*/

@description('Region for APIM.')
param location string

@description('Globally unique APIM service name. Resolves to <name>.azure-api.net.')
param apimName string

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
    publicNetworkAccess: 'Enabled'
  }
}

output apimResourceId string = apim.id
output apimName string = apim.name
output apimGatewayUrl string = apim.properties.gatewayUrl
output apimPrincipalId string = apim.identity.principalId
