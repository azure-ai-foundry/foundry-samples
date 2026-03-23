# Example: Azure AI Search Connection
# This example shows how to create a connection from AI Foundry to Azure AI Search

terraform {
  required_providers {
    azapi = {
      source  = "azure/azapi"
      version = "~> 2.5"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.37"
    }
  }
}

provider "azapi" {}
provider "azurerm" {
  features {}
}

variable "ai_foundry_name" {
  description = "Name of your existing AI Foundry account"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group containing AI Foundry account"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "westus"
}

variable "create_new_search" {
  description = "Create new AI Search service or use existing"
  type        = bool
  default     = true
}

variable "search_service_name" {
  description = "Name of the AI Search service"
  type        = string
}

# Reference existing AI Foundry account
data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

data "azapi_resource" "ai_foundry" {
  type      = "Microsoft.CognitiveServices/accounts@2025-04-01-preview"
  name      = var.ai_foundry_name
  parent_id = data.azurerm_resource_group.rg.id
}

# Conditionally create new AI Search service
resource "azurerm_search_service" "search" {
  count               = var.create_new_search ? 1 : 0
  name                = var.search_service_name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "basic"
}

# Reference existing AI Search service
data "azurerm_search_service" "existing" {
  count               = var.create_new_search ? 0 : 1
  name                = var.search_service_name
  resource_group_name = var.resource_group_name
}

locals {
  search_endpoint = var.create_new_search ? "https://${azurerm_search_service.search[0].name}.search.windows.net" : "https://${data.azurerm_search_service.existing[0].name}.search.windows.net"
  search_id       = var.create_new_search ? azurerm_search_service.search[0].id : data.azurerm_search_service.existing[0].id
  search_primary_key = var.create_new_search ? azurerm_search_service.search[0].primary_key : data.azurerm_search_service.existing[0].primary_key
}

# Create AI Search connection
resource "azapi_resource" "ai_search_connection" {
  type      = "Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview"
  name      = "${var.ai_foundry_name}-aisearch"
  parent_id = data.azapi_resource.ai_foundry.id

  body = {
    properties = {
      category      = "CognitiveSearch"
      target        = local.search_endpoint
      authType      = "ApiKey"
      isSharedToAll = true
      credentials = {
        key = local.search_primary_key
      }
      metadata = {
        ApiType    = "Azure"
        ResourceId = local.search_id
        location   = var.location
      }
    }
  }
}

output "connection_id" {
  value = azapi_resource.ai_search_connection.id
}
