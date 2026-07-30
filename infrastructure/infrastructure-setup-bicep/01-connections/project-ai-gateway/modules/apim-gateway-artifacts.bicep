/*
  Per-project APIM artifacts for enabling a Foundry project on an AI Gateway.
  Deployed into the APIM service's resource group / subscription.

  Creates:
    - APIM Product     : subscriptionRequired = true, state = 'published'
    - Product <-> API  : associate the gateway's shared API (named after the account)
    - APIM Subscription: scoped to the product, state = 'active', allowTracing = false

  The ARM resource link (project -> product) that marks the project Enabled is
  created by the parent template at the project's scope.
*/

@description('Name of the existing APIM service backing the gateway.')
param apimServiceName string

@description('Name of the per-project APIM product to create.')
param productName string

@description('Id of the gateway shared API to associate with the product (named after the Foundry account).')
param sharedApiId string

resource apim 'Microsoft.ApiManagement/service@2022-08-01' existing = {
  name: apimServiceName
}

// a. Per-project product (subscription required, published).
resource product 'Microsoft.ApiManagement/service/products@2022-08-01' = {
  parent: apim
  name: productName
  properties: {
    displayName: productName
    subscriptionRequired: true
    state: 'published'
  }
}

// b. Associate the gateway's shared API with the product.
resource productApi 'Microsoft.ApiManagement/service/products/apis@2022-08-01' = {
  parent: product
  name: sharedApiId
}

// c. Active subscription scoped to the product.
resource productSubscription 'Microsoft.ApiManagement/service/subscriptions@2022-08-01' = {
  parent: apim
  name: productName
  properties: {
    displayName: productName
    scope: product.id
    state: 'active'
    allowTracing: false
  }
}

@description('Resource ID of the created product.')
output productId string = product.id
