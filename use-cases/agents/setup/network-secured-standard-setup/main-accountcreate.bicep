// Standard agent setup 

@description('Resource group location')
param resourceGroupLocation string = resourceGroup().location

@allowed([
    'australiaeast'
    'eastus'
    'eastus2'
    'francecentral'
    'japaneast'
    'norwayeast'
    'southindia'
    'swedencentral'
    'uaenorth'
    'uksouth'
    'westus'
    'westus3'
  ])
@description('Location for all resources.')
param location string = resourceGroupLocation

@description('Name for your AI Services resource.')
param aiServices string = 'aiservices'

// // Model deployment parameters
// @description('The name of the model you want to deploy')
// param modelName string = 'gpt-4o-mini'
// @description('The provider of your model')
// param modelFormat string = 'OpenAI'
// @description('The version of your model')
// param modelVersion string = '2024-05-13'
// @description('The sku of your model deployment')
// param modelSkuName string = 'GlobalStandard'
// @description('The tokens per minute (TPM) of your model deployment')
// param modelCapacity int = 1


// Create a short, unique suffix, that will be unique to each resource group
param deploymentTimestamp string = utcNow('yyyyMMddHHmmss')
var uniqueSuffix = substring(uniqueString('${resourceGroup().id}-${deploymentTimestamp}'), 0, 4)
var accountName = toLower('${aiServices}${uniqueSuffix}')


// Create Virtual Network and Subnets
module vnet 'modules-network-secured/vnet.bicep' = {
    name: '${uniqueSuffix}-vnet'
    params: {
      location: location
    }
  }

/*
  Create the AI Services account and gpt-4o model deployment
*/
module aiAccount 'modules-network-secured/ai-account-identity.bicep' = {
  name: 'ai-${accountName}-${uniqueSuffix}-deployment'
  params: {
    // workspace organization
    accountName: accountName
    location: location
    // modelName: modelName
    // modelFormat: modelFormat
    // modelVersion: modelVersion
    // modelSkuName: modelSkuName
    // modelCapacity: modelCapacity
    subnetId: vnet.outputs.subnetId
  }
}

output accountName string = aiAccount.outputs.accountName
output subscriptionID string = subscription().subscriptionId
output resourceGroupName string = resourceGroup().name
output vnetName string = vnet.outputs.virtualNetworkName
output peSubnetName string = vnet.outputs.peSubnetName
