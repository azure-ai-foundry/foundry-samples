# Azure Monitor Private Link Scope (AMPLS) for Application Insights telemetry
# This enables hosted agents in the managed VNet to export telemetry to App Insights

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  count               = var.enable_networking ? 1 : 0
  name                = "law-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = var.tags
}

# Application Insights
resource "azurerm_application_insights" "main" {
  count               = var.enable_networking ? 1 : 0
  name                = "appi-${local.resource_suffix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main[0].id
  application_type    = "web"

  tags = var.tags
}

# Azure Monitor Private Link Scope
resource "azurerm_monitor_private_link_scope" "main" {
  count               = var.enable_networking ? 1 : 0
  name                = "ampls-${local.resource_suffix}"
  resource_group_name = azurerm_resource_group.main.name

  tags = var.tags
}

# Link App Insights to AMPLS
resource "azurerm_monitor_private_link_scoped_service" "app_insights" {
  count               = var.enable_networking ? 1 : 0
  name                = "ampls-appinsights-link"
  resource_group_name = azurerm_resource_group.main.name
  scope_name          = azurerm_monitor_private_link_scope.main[0].name
  linked_resource_id  = azurerm_application_insights.main[0].id
}

# Link Log Analytics to AMPLS
resource "azurerm_monitor_private_link_scoped_service" "log_analytics" {
  count               = var.enable_networking ? 1 : 0
  name                = "ampls-loganalytics-link"
  resource_group_name = azurerm_resource_group.main.name
  scope_name          = azurerm_monitor_private_link_scope.main[0].name
  linked_resource_id  = azurerm_log_analytics_workspace.main[0].id
}

# Private DNS Zones for Azure Monitor
resource "azurerm_private_dns_zone" "monitor" {
  count               = var.enable_dns ? 1 : 0
  name                = "privatelink.monitor.azure.com"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_zone" "oms" {
  count               = var.enable_dns ? 1 : 0
  name                = "privatelink.oms.opinsights.azure.com"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_zone" "ods" {
  count               = var.enable_dns ? 1 : 0
  name                = "privatelink.ods.opinsights.azure.com"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_zone" "agentsvc" {
  count               = var.enable_dns ? 1 : 0
  name                = "privatelink.agentsvc.azure-automation.net"
  resource_group_name = azurerm_resource_group.main.name
}

# VNet Links for Monitor DNS Zones
resource "azurerm_private_dns_zone_virtual_network_link" "monitor" {
  count                 = var.enable_dns && var.enable_networking ? 1 : 0
  name                  = "${var.vnet_name}-monitor-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.monitor[0].name
  virtual_network_id    = azurerm_virtual_network.main[0].id
}

resource "azurerm_private_dns_zone_virtual_network_link" "oms" {
  count                 = var.enable_dns && var.enable_networking ? 1 : 0
  name                  = "${var.vnet_name}-oms-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.oms[0].name
  virtual_network_id    = azurerm_virtual_network.main[0].id
}

resource "azurerm_private_dns_zone_virtual_network_link" "ods" {
  count                 = var.enable_dns && var.enable_networking ? 1 : 0
  name                  = "${var.vnet_name}-ods-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.ods[0].name
  virtual_network_id    = azurerm_virtual_network.main[0].id
}

resource "azurerm_private_dns_zone_virtual_network_link" "agentsvc" {
  count                 = var.enable_dns && var.enable_networking ? 1 : 0
  name                  = "${var.vnet_name}-agentsvc-link"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.agentsvc[0].name
  virtual_network_id    = azurerm_virtual_network.main[0].id
}

# Private Endpoint for AMPLS
resource "azurerm_private_endpoint" "ampls" {
  count               = var.enable_networking ? 1 : 0
  name                = "ampls-${local.resource_suffix}-pe"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  subnet_id           = azurerm_subnet.private_endpoints[0].id

  private_service_connection {
    name                           = "ampls-psc"
    private_connection_resource_id = azurerm_monitor_private_link_scope.main[0].id
    is_manual_connection           = false
    subresource_names              = ["azuremonitor"]
  }

  dynamic "private_dns_zone_group" {
    for_each = var.enable_dns ? [1] : []
    content {
      name = "ampls-dns-zone-group"
      private_dns_zone_ids = [
        azurerm_private_dns_zone.monitor[0].id,
        azurerm_private_dns_zone.oms[0].id,
        azurerm_private_dns_zone.ods[0].id,
        azurerm_private_dns_zone.agentsvc[0].id
      ]
    }
  }

  depends_on = [
    azurerm_monitor_private_link_scoped_service.app_insights,
    azurerm_monitor_private_link_scoped_service.log_analytics
  ]
}

# Managed Network Outbound Rule for AMPLS
# This creates a PE from the Foundry managed VNet to AMPLS
resource "azapi_resource" "ampls_outbound_rule" {
  count     = var.enable_networking ? 1 : 0
  type      = "Microsoft.CognitiveServices/accounts/managedNetworks/outboundRules@2025-10-01-preview"
  name      = "ampls-monitor-rule"
  parent_id = azapi_resource.managed_network.id

  schema_validation_enabled = false

  body = {
    properties = {
      type = "PrivateEndpoint"
      destination = {
        serviceResourceId = azurerm_monitor_private_link_scope.main[0].id
        subresourceTarget = "azuremonitor"
      }
      category = "UserDefined"
    }
  }

  depends_on = [
    azapi_resource.managed_network,
    azapi_resource.aiservices_outbound_rule,
    azurerm_role_assignment.foundry_network_connection_approver,
    azurerm_private_endpoint.ampls
  ]
}

# App Insights Connection on the Foundry Project
resource "azapi_resource" "conn_app_insights" {
  count     = var.enable_networking ? 1 : 0
  type      = "Microsoft.CognitiveServices/accounts/projects/connections@2025-04-01-preview"
  name      = azurerm_application_insights.main[0].name
  parent_id = azapi_resource.ai_foundry_project.id

  schema_validation_enabled = false

  body = {
    properties = {
      category = "AppInsights"
      target   = azurerm_application_insights.main[0].id
      authType = "ApiKey"
      credentials = {
        key = azurerm_application_insights.main[0].connection_string
      }
      metadata = {
        ApiType    = "Azure"
        ResourceId = azurerm_application_insights.main[0].id
      }
    }
  }

  depends_on = [
    azapi_resource.conn_storage
  ]
}
