##############################################################################
# Azure Function App with VNet Integration and Private Endpoint
#
# Deploys a minimal Python Azure Function with VNet networking:
#   - VNet Integration for outbound traffic (function can reach private resources)
#   - Private Endpoint for inbound access from VNet callers
#   - Private DNS zone for privatelink.azurewebsites.net
#   - Storage Private Endpoints (Blob + Queue + File — required for Functions runtime)
#
# IMPORTANT: publicNetworkAccess is set to 'Enabled' because the Foundry DataProxy
# resolves DNS at the infrastructure level, not through VNet private DNS zones.
# Setting it to 'Disabled' causes 403 Ip Forbidden errors when the Function is used
# as an OpenAPI tool. Use App Service access restrictions for inbound IP filtering.
#
# Usage:
#   terraform init
#   terraform plan -var="vnet_name=<vnet>" -var="pe_subnet_name=<subnet>" -out=tfplan
#   terraform apply tfplan
##############################################################################

terraform {
  required_version = ">= 1.10.0, < 2.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.37"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.7"
    }
  }
}

provider "azurerm" {
  features {}
  resource_provider_registrations = "none"
}

###############################################################################
# Variables
###############################################################################

variable "location" {
  type        = string
  description = "Azure region for all resources."
  default     = "swedencentral"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the existing resource group (from the base TF 19 deployment)."
}

variable "vnet_name" {
  type        = string
  description = "Name of the existing VNet (from the base TF 19 deployment)."
}

variable "pe_subnet_name" {
  type        = string
  description = "Name of the existing private endpoint subnet."
  default     = "pe-subnet"
}

variable "integration_subnet_name" {
  type        = string
  description = "Name for the Function App VNet Integration subnet (delegated to Microsoft.Web/serverFarms)."
  default     = "func-integration-subnet"
}

variable "integration_subnet_prefix" {
  type        = string
  description = "Address prefix for the integration subnet."
  default     = "192.168.5.0/24"
}

variable "base_name" {
  type        = string
  description = "Base name prefix for Function App resources. A random suffix is appended."
  default     = "functest"
}

###############################################################################
# Data Sources — existing resources from the base deployment
###############################################################################

data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

data "azurerm_virtual_network" "vnet" {
  name                = var.vnet_name
  resource_group_name = var.resource_group_name
}

data "azurerm_subnet" "pe" {
  name                 = var.pe_subnet_name
  virtual_network_name = var.vnet_name
  resource_group_name  = var.resource_group_name
}

resource "random_string" "suffix" {
  length  = 4
  lower   = true
  upper   = false
  special = false
  numeric = true
}

locals {
  name_prefix = "${var.base_name}${random_string.suffix.result}"
}

###############################################################################
# Integration Subnet (delegated to Microsoft.Web/serverFarms)
###############################################################################

resource "azurerm_subnet" "func_integration" {
  name                 = var.integration_subnet_name
  resource_group_name  = var.resource_group_name
  virtual_network_name = var.vnet_name
  address_prefixes     = [var.integration_subnet_prefix]

  delegation {
    name = "Microsoft.Web.serverFarms"

    service_delegation {
      name    = "Microsoft.Web/serverFarms"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

###############################################################################
# Storage Account (required by Functions runtime)
#
# NOTE: Storage must be accessible during Function App creation (file share).
# Deploy with default_action = "Allow" first, then restrict after Function App
# is created. After restricting, restart the Function App to avoid 503 errors.
###############################################################################

resource "azurerm_storage_account" "func" {
  name                          = "${substr(local.name_prefix, 0, 20)}stor"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  account_tier                  = "Standard"
  account_replication_type      = "LRS"
  account_kind                  = "StorageV2"
  https_traffic_only_enabled    = true
  allow_nested_items_to_be_public = false
  public_network_access_enabled = true

  network_rules {
    default_action = "Allow"
  }
}

###############################################################################
# Storage Private Endpoints (Blob + Queue + File — required for Functions)
#
# The Functions runtime needs all three: Blob for triggers/bindings,
# Queue for internal messaging, File for the content share (WEBSITE_CONTENTSHARE).
###############################################################################

resource "azurerm_private_endpoint" "storage_blob" {
  name                = "${local.name_prefix}-blob-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = data.azurerm_subnet.pe.id

  private_service_connection {
    name                           = "blob"
    private_connection_resource_id = azurerm_storage_account.func.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.storage_blob.id]
  }
}

resource "azurerm_private_endpoint" "storage_queue" {
  name                = "${local.name_prefix}-queue-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = data.azurerm_subnet.pe.id

  private_service_connection {
    name                           = "queue"
    private_connection_resource_id = azurerm_storage_account.func.id
    subresource_names              = ["queue"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [azurerm_private_dns_zone.storage_queue.id]
  }
}

resource "azurerm_private_endpoint" "storage_file" {
  name                = "${local.name_prefix}-file-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = data.azurerm_subnet.pe.id

  private_service_connection {
    name                           = "file"
    private_connection_resource_id = azurerm_storage_account.func.id
    subresource_names              = ["file"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [azurerm_private_dns_zone.storage_file.id]
  }
}

###############################################################################
# Storage File Share (required for WEBSITE_CONTENTSHARE over VNet)
###############################################################################

resource "azurerm_storage_share" "func_content" {
  name               = "${local.name_prefix}-content"
  storage_account_id = azurerm_storage_account.func.id
  quota              = 1
}

###############################################################################
# App Service Plan (Elastic Premium for VNet features)
###############################################################################

resource "azurerm_service_plan" "func" {
  name                = "${local.name_prefix}-plan"
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Linux"
  sku_name            = "EP1"
}

###############################################################################
# Function App
#
# publicNetworkAccess must be 'Enabled' for DataProxy compatibility.
# The DataProxy resolves DNS at the Foundry infrastructure level, not through
# VNet private DNS zones. 'Disabled' causes 403 Ip Forbidden errors.
###############################################################################

resource "azurerm_linux_function_app" "func" {
  name                          = "${local.name_prefix}-func"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  service_plan_id               = azurerm_service_plan.func.id
  storage_account_name          = azurerm_storage_account.func.name
  storage_account_access_key    = azurerm_storage_account.func.primary_access_key
  virtual_network_subnet_id     = azurerm_subnet.func_integration.id
  public_network_access_enabled = true

  site_config {
    application_stack {
      python_version = "3.11"
    }

    vnet_route_all_enabled = true
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME    = "python"
    FUNCTIONS_EXTENSION_VERSION = "~4"
    WEBSITE_CONTENTOVERVNET     = "1"
    WEBSITE_VNET_ROUTE_ALL     = "1"
    WEBSITE_CONTENTSHARE        = azurerm_storage_share.func_content.name
  }
}

###############################################################################
# Function App Private Endpoint + DNS
###############################################################################

resource "azurerm_private_dns_zone" "azurewebsites" {
  name                = "privatelink.azurewebsites.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "azurewebsites" {
  name                  = "${var.vnet_name}-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.azurewebsites.name
  virtual_network_id    = data.azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

resource "azurerm_private_endpoint" "func" {
  name                = "${local.name_prefix}-func-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = data.azurerm_subnet.pe.id

  private_service_connection {
    name                           = "sites"
    private_connection_resource_id = azurerm_linux_function_app.func.id
    subresource_names              = ["sites"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [azurerm_private_dns_zone.azurewebsites.id]
  }
}

###############################################################################
# Storage DNS Zones
#
# Blob zone is expected to exist from the base TF 19 deployment (with VNet link).
# Queue and File zones are created here (only needed for Functions runtime).
###############################################################################

data "azurerm_private_dns_zone" "storage_blob" {
  name                = "privatelink.blob.core.windows.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone" "storage_queue" {
  name                = "privatelink.queue.core.windows.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "storage_queue" {
  name                  = "${var.vnet_name}-queue-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.storage_queue.name
  virtual_network_id    = data.azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

resource "azurerm_private_dns_zone" "storage_file" {
  name                = "privatelink.file.core.windows.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "storage_file" {
  name                  = "${var.vnet_name}-file-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.storage_file.name
  virtual_network_id    = data.azurerm_virtual_network.vnet.id
  registration_enabled  = false
}

###############################################################################
# Outputs
###############################################################################

output "function_app_name" {
  value = azurerm_linux_function_app.func.name
}

output "function_app_hostname" {
  value = azurerm_linux_function_app.func.default_hostname
}

output "function_private_endpoint_id" {
  value = azurerm_private_endpoint.func.id
}

output "function_app_resource_id" {
  value = azurerm_linux_function_app.func.id
}
