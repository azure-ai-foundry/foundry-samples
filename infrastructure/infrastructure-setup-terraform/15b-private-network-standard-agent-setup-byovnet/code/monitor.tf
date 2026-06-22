########## Create Azure Monitor resources for agent tracing
##########
## Deploys Application Insights and an Azure Monitor Private Link Scope (AMPLS) so the
## hosted agent in the VNet can export OpenTelemetry traces to Application Insights over
## the private network instead of having the spans dropped. As with the other resources in
## this Bring-Your-Own-VNet template, the Azure Monitor Private DNS Zones are expected to
## already exist in the infrastructure subscription and are referenced by resource id.

## Create the Log Analytics workspace that backs Application Insights
##
resource "azurerm_log_analytics_workspace" "loganalytics" {
  provider = azurerm.workload_subscription

  name                = "loganalytics-tracing-${random_string.unique.result}"
  location            = var.location
  resource_group_name = var.resource_group_name_resources
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

## Create the workspace-based Application Insights. Public ingestion is disabled; traces
## are ingested privately through the Azure Monitor Private Link Scope below.
##
resource "azurerm_application_insights" "app_insights" {
  provider = azurerm.workload_subscription

  name                       = "appinsights-tracing-${random_string.unique.result}"
  location                   = var.location
  resource_group_name        = var.resource_group_name_resources
  workspace_id               = azurerm_log_analytics_workspace.loganalytics.id
  application_type           = "web"
  internet_ingestion_enabled = false
  internet_query_enabled     = true
}

## Create the Azure Monitor Private Link Scope (private ingestion, open query)
##
resource "azurerm_monitor_private_link_scope" "ampls" {
  provider = azurerm.workload_subscription

  name                  = "ampls-tracing-${random_string.unique.result}"
  resource_group_name   = var.resource_group_name_resources
  ingestion_access_mode = "PrivateOnly"
  query_access_mode     = "Open"
}

## Scope Application Insights and the Log Analytics workspace into the AMPLS
##
resource "azurerm_monitor_private_link_scoped_service" "ampls_app_insights" {
  provider = azurerm.workload_subscription

  name                = "appinsights-scoped"
  resource_group_name = var.resource_group_name_resources
  scope_name          = azurerm_monitor_private_link_scope.ampls.name
  linked_resource_id  = azurerm_application_insights.app_insights.id
}

resource "azurerm_monitor_private_link_scoped_service" "ampls_loganalytics" {
  provider = azurerm.workload_subscription

  name                = "loganalytics-scoped"
  resource_group_name = var.resource_group_name_resources
  scope_name          = azurerm_monitor_private_link_scope.ampls.name
  linked_resource_id  = azurerm_log_analytics_workspace.loganalytics.id
}

## Create the Private Endpoint for the AMPLS (group "azuremonitor"). The Azure Monitor
## Private DNS Zones are expected to already exist in the infrastructure subscription.
##
resource "azurerm_private_endpoint" "pe_ampls" {
  provider = azurerm.workload_subscription

  depends_on = [
    azurerm_monitor_private_link_scoped_service.ampls_app_insights,
    azurerm_monitor_private_link_scoped_service.ampls_loganalytics
  ]

  name                = "ampls-tracing-${random_string.unique.result}-private-endpoint"
  location            = var.location
  resource_group_name = var.resource_group_name_resources
  subnet_id           = var.subnet_id_private_endpoint

  private_service_connection {
    name                           = "ampls-tracing-private-link-service-connection"
    private_connection_resource_id = azurerm_monitor_private_link_scope.ampls.id
    subresource_names = [
      "azuremonitor"
    ]
    is_manual_connection = false
  }

  private_dns_zone_group {
    name = "ampls-tracing-dns-config"
    private_dns_zone_ids = [
      "/subscriptions/${var.subscription_id_infra}/resourceGroups/${var.resource_group_name_dns}/providers/Microsoft.Network/privateDnsZones/privatelink.monitor.azure.com",
      "/subscriptions/${var.subscription_id_infra}/resourceGroups/${var.resource_group_name_dns}/providers/Microsoft.Network/privateDnsZones/privatelink.oms.opinsights.azure.com",
      "/subscriptions/${var.subscription_id_infra}/resourceGroups/${var.resource_group_name_dns}/providers/Microsoft.Network/privateDnsZones/privatelink.ods.opinsights.azure.com",
      "/subscriptions/${var.subscription_id_infra}/resourceGroups/${var.resource_group_name_dns}/providers/Microsoft.Network/privateDnsZones/privatelink.agentsvc.azure-automation.net"
    ]
  }
}

## Create the AI Foundry project connection to Application Insights so the agent exports
## its OpenTelemetry traces here
##
resource "azapi_resource" "conn_app_insights" {
  provider = azapi.workload_subscription

  type                      = "Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01"
  name                      = azurerm_application_insights.app_insights.name
  parent_id                 = azapi_resource.ai_foundry_project.id
  schema_validation_enabled = false

  depends_on = [
    azapi_resource.ai_foundry_project
  ]

  body = {
    name = azurerm_application_insights.app_insights.name
    properties = {
      category = "AppInsights"
      target   = azurerm_application_insights.app_insights.id
      authType = "ApiKey"
      credentials = {
        key = azurerm_application_insights.app_insights.connection_string
      }
      metadata = {
        ApiType    = "Azure"
        ResourceId = azurerm_application_insights.app_insights.id
        location   = var.location
      }
    }
  }
}
