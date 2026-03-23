output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "user_assigned_identity_id" {
  description = "The ID of the User-Assigned Identity"
  value       = local.uai_id
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

output "private_endpoint_ips" {
  description = "Private endpoint IP addresses"
  value = {
    ai_foundry = azurerm_private_endpoint.ai_foundry.private_service_connection[0].private_ip_address
    storage    = azurerm_private_endpoint.storage.private_service_connection[0].private_ip_address
    search     = azurerm_private_endpoint.search.private_service_connection[0].private_ip_address
    cosmos     = azurerm_private_endpoint.cosmos.private_service_connection[0].private_ip_address
  }
}
