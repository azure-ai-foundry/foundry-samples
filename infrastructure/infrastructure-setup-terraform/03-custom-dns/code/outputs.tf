output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "ai_foundry_id" {
  description = "The ID of the AI Foundry account"
  value       = azapi_resource.ai_foundry.id
}

output "custom_endpoint" {
  description = "The custom API endpoint"
  value       = "https://${var.custom_subdomain_name}.cognitiveservices.azure.com/"
}

output "ai_project_id" {
  description = "The ID of the AI Foundry project"
  value       = azapi_resource.ai_project.id
}
