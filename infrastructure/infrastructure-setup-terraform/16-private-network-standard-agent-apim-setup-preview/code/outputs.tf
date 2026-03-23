output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "vnet_id" {
  description = "The ID of the virtual network"
  value       = azurerm_virtual_network.vnet.id
}

output "ai_foundry_id" {
  description = "The ID of the AI Foundry account"
  value       = azapi_resource.ai_foundry.id
}

output "ai_project_id" {
  description = "The ID of the AI Foundry project"
  value       = azapi_resource.ai_project.id
}

output "storage_account_id" {
  description = "The ID of the storage account"
  value       = azurerm_storage_account.storage.id
}

output "search_service_id" {
  description = "The ID of the AI Search service"
  value       = azurerm_search_service.search.id
}

output "cosmos_db_id" {
  description = "The ID of the Cosmos DB account"
  value       = azurerm_cosmosdb_account.cosmos.id
}

output "apim_id" {
  description = "The ID of the API Management instance"
  value       = azurerm_api_management.apim.id
}

output "apim_gateway_url" {
  description = "The gateway URL of the API Management instance"
  value       = azurerm_api_management.apim.gateway_url
}

output "apim_portal_url" {
  description = "The portal URL of the API Management instance"
  value       = azurerm_api_management.apim.portal_url
}

output "notes" {
  description = "Important notes about this deployment"
  value       = "This is a complex scenario. Additional APIM API configuration, policies, and subscriptions need to be configured manually or via additional Terraform resources."
}
