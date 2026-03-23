output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "ai_foundry_id" {
  description = "The ID of the AI Foundry account"
  value       = azapi_resource.ai_foundry.id
}

output "ai_project_id" {
  description = "The ID of the AI Foundry project"
  value       = azapi_resource.ai_project.id
}

output "storage_account_name" {
  description = "The name of the storage account"
  value       = azurerm_storage_account.storage.name
}

output "search_service_name" {
  description = "The name of the search service"
  value       = azurerm_search_service.search.name
}

output "cosmos_account_name" {
  description = "The name of the Cosmos DB account"
  value       = azurerm_cosmosdb_account.cosmos.name
}
