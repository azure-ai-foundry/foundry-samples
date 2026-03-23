output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "ai_foundry_id" {
  description = "The ID of the AI Foundry account"
  value       = azapi_resource.ai_foundry.id
}

output "ai_foundry_name" {
  description = "The name of the AI Foundry account"
  value       = var.ai_foundry_name
}

output "storage_account_id" {
  description = "The ID of the storage account"
  value       = local.storage_id
}
