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

output "bing_connection_id" {
  description = "The ID of the Bing connection"
  value       = azapi_resource.bing_connection.id
}

output "model_deployment_name" {
  description = "The name of the model deployment"
  value       = azapi_resource.model_deployment.name
}
