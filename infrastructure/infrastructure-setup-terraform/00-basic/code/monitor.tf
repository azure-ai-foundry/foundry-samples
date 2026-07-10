########## Create Application Insights resources for agent tracing
##########

## Create the Log Analytics workspace that backs Application Insights
##
resource "azurerm_log_analytics_workspace" "loganalytics" {
  name                = "law-${azapi_resource.ai_foundry.name}"
  location            = var.location
  resource_group_name = azapi_resource.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

## Create workspace-based Application Insights and connect it to the Foundry account
##
resource "azurerm_application_insights" "app_insights" {
  name                = "appi-${azapi_resource.ai_foundry.name}"
  location            = var.location
  resource_group_name = azapi_resource.rg.name
  workspace_id        = azurerm_log_analytics_workspace.loganalytics.id
  application_type    = "web"
}

## Create the Foundry account connection to Application Insights so tracing is available
##
resource "azapi_resource" "app_insights_connection" {
  type      = "Microsoft.CognitiveServices/accounts/connections@2025-06-01"
  name      = "${azapi_resource.ai_foundry.name}-appinsights"
  parent_id = azapi_resource.ai_foundry.id

  body = {
    properties = {
      category      = "AppInsights"
      target        = azurerm_application_insights.app_insights.id
      authType      = "ApiKey"
      isSharedToAll = true
      credentials = {
        key = azurerm_application_insights.app_insights.connection_string
      }
      metadata = {
        ApiType    = "Azure"
        ResourceId = azurerm_application_insights.app_insights.id
      }
    }
  }
}
