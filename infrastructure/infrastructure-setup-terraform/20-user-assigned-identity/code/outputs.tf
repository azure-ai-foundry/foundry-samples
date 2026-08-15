output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "user_assigned_identity_id" {
  description = "The ID of the User-Assigned Identity"
  value       = local.uai_id
}

output "ai_foundry_id" {
  description = "The ID of the AI Foundry account"
  value       = azapi_resource.ai_foundry.id
}

output "ai_project_id" {
  description = "The ID of the AI Foundry project"
  value       = azapi_resource.ai_project.id
}

output "application_insights_id" {
  description = "The ID of the Application Insights component"
  value       = azurerm_application_insights.app_insights.id
}

output "application_insights_app_id" {
  description = "The application ID of the Application Insights component"
  value       = azurerm_application_insights.app_insights.app_id
}

output "application_insights_connection_name" {
  description = "The name of the Application Insights project connection"
  value       = azapi_resource.application_insights_connection.name
}

output "application_insights_connection_auth_type" {
  description = "The authentication type used by the Application Insights project connection"
  value       = var.use_project_managed_identity_for_trace_ingestion ? "ProjectManagedIdentity" : "ApiKey"
}

output "log_analytics_workspace_id" {
  description = "The ID of the Log Analytics workspace"
  value       = azurerm_log_analytics_workspace.log_analytics.id
}
