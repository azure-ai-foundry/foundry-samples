output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "virtual_network_id" {
  description = "The ID of the virtual network"
  value       = azurerm_virtual_network.vnet.id
}

output "ai_foundry_id" {
  description = "The ID of the AI Foundry account"
  value       = azapi_resource.ai_foundry.id
}

output "ai_foundry_name" {
  description = "The name of the AI Foundry account"
  value       = azapi_resource.ai_foundry.name
}

output "ai_project_id" {
  description = "The ID of the AI Foundry project"
  value       = azapi_resource.ai_foundry_project.id
}

output "private_endpoint_id" {
  description = "The ID of the AI Foundry private endpoint"
  value       = azurerm_private_endpoint.pe_aifoundry.id
}

output "acr_id" {
  description = "The ID of the Azure Container Registry (if created)"
  value       = var.enable_container_registry ? azurerm_container_registry.acr[0].id : null
}
