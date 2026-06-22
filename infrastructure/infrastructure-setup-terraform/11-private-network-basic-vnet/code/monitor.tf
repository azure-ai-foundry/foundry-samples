########## Create Azure Monitor resources for agent tracing
##########
## Deploys Application Insights and an Azure Monitor Private Link Scope (AMPLS) so the
## hosted agent in the VNet can export OpenTelemetry traces to Application Insights over
## the private network instead of having the spans dropped.

## Create the Log Analytics workspace that backs Application Insights
##
resource "azurerm_log_analytics_workspace" "loganalytics" {
  name                = "loganalytics-tracing-${random_string.unique.result}"
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

## Create the workspace-based Application Insights. Public ingestion is disabled; traces
## are ingested privately through the Azure Monitor Private Link Scope below.
##
resource "azurerm_application_insights" "app_insights" {
  name                       = "appinsights-tracing-${random_string.unique.result}"
  location                   = var.location
  resource_group_name        = azurerm_resource_group.rg.name
  workspace_id               = azurerm_log_analytics_workspace.loganalytics.id
  application_type           = "web"
  internet_ingestion_enabled = false
  internet_query_enabled     = true
}

## Create the Azure Monitor Private Link Scope (private ingestion, open query)
##
resource "azurerm_monitor_private_link_scope" "ampls" {
  name                  = "ampls-tracing-${random_string.unique.result}"
  resource_group_name   = azurerm_resource_group.rg.name
  ingestion_access_mode = "PrivateOnly"
  query_access_mode     = "Open"
}

## Scope Application Insights and the Log Analytics workspace into the AMPLS
##
resource "azurerm_monitor_private_link_scoped_service" "ampls_app_insights" {
  name                = "appinsights-scoped"
  resource_group_name = azurerm_resource_group.rg.name
  scope_name          = azurerm_monitor_private_link_scope.ampls.name
  linked_resource_id  = azurerm_application_insights.app_insights.id
}

resource "azurerm_monitor_private_link_scoped_service" "ampls_loganalytics" {
  name                = "loganalytics-scoped"
  resource_group_name = azurerm_resource_group.rg.name
  scope_name          = azurerm_monitor_private_link_scope.ampls.name
  linked_resource_id  = azurerm_log_analytics_workspace.loganalytics.id
}

## Create the Azure Monitor Private DNS Zones
##
resource "azurerm_private_dns_zone" "plz_monitor" {
  name                = "privatelink.monitor.azure.com"
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_private_dns_zone" "plz_oms" {
  name                = "privatelink.oms.opinsights.azure.com"
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_private_dns_zone" "plz_ods" {
  name                = "privatelink.ods.opinsights.azure.com"
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_private_dns_zone" "plz_agentsvc" {
  name                = "privatelink.agentsvc.azure-automation.net"
  resource_group_name = azurerm_resource_group.rg.name
}

## Create Private DNS Zone Links to link the Azure Monitor zones to the virtual network
##
resource "azurerm_private_dns_zone_virtual_network_link" "plz_monitor_link" {
  depends_on = [
    azurerm_private_dns_zone.plz_monitor,
    azurerm_virtual_network.vnet
  ]
  name                  = "monitor-${random_string.unique.result}-link"
  resource_group_name   = azurerm_resource_group.rg.name
  private_dns_zone_name = azurerm_private_dns_zone.plz_monitor.name
  virtual_network_id    = azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

resource "azurerm_private_dns_zone_virtual_network_link" "plz_oms_link" {
  depends_on = [
    azurerm_private_dns_zone_virtual_network_link.plz_monitor_link,
    azurerm_private_dns_zone.plz_oms,
    azurerm_virtual_network.vnet
  ]
  name                  = "oms-${random_string.unique.result}-link"
  resource_group_name   = azurerm_resource_group.rg.name
  private_dns_zone_name = azurerm_private_dns_zone.plz_oms.name
  virtual_network_id    = azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

resource "azurerm_private_dns_zone_virtual_network_link" "plz_ods_link" {
  depends_on = [
    azurerm_private_dns_zone_virtual_network_link.plz_oms_link,
    azurerm_private_dns_zone.plz_ods,
    azurerm_virtual_network.vnet
  ]
  name                  = "ods-${random_string.unique.result}-link"
  resource_group_name   = azurerm_resource_group.rg.name
  private_dns_zone_name = azurerm_private_dns_zone.plz_ods.name
  virtual_network_id    = azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

resource "azurerm_private_dns_zone_virtual_network_link" "plz_agentsvc_link" {
  depends_on = [
    azurerm_private_dns_zone_virtual_network_link.plz_ods_link,
    azurerm_private_dns_zone.plz_agentsvc,
    azurerm_virtual_network.vnet
  ]
  name                  = "agentsvc-${random_string.unique.result}-link"
  resource_group_name   = azurerm_resource_group.rg.name
  private_dns_zone_name = azurerm_private_dns_zone.plz_agentsvc.name
  virtual_network_id    = azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

## Create the Private Endpoint for the AMPLS (group "azuremonitor")
##
resource "azurerm_private_endpoint" "pe_ampls" {
  depends_on = [
    azurerm_monitor_private_link_scoped_service.ampls_app_insights,
    azurerm_monitor_private_link_scoped_service.ampls_loganalytics,
    azurerm_private_dns_zone_virtual_network_link.plz_monitor_link,
    azurerm_private_dns_zone_virtual_network_link.plz_oms_link,
    azurerm_private_dns_zone_virtual_network_link.plz_ods_link,
    azurerm_private_dns_zone_virtual_network_link.plz_agentsvc_link,
    azurerm_virtual_network.vnet
  ]

  name                = "ampls-tracing-${random_string.unique.result}-private-endpoint"
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  subnet_id           = azurerm_subnet.subnet_pe.id

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
      azurerm_private_dns_zone.plz_monitor.id,
      azurerm_private_dns_zone.plz_oms.id,
      azurerm_private_dns_zone.plz_ods.id,
      azurerm_private_dns_zone.plz_agentsvc.id
    ]
  }
}

## Create the AI Foundry project connection to Application Insights so the agent exports
## its OpenTelemetry traces here
##
resource "azapi_resource" "conn_app_insights" {
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
