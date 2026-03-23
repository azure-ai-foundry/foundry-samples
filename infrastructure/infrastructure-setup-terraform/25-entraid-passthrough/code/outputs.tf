output "resource_group_name" {
  value = azurerm_resource_group.rg.name
}

output "ai_foundry_id" {
  value = azapi_resource.ai_foundry.id
}

output "ai_project_id" {
  value = azapi_resource.ai_project.id
}
