# Example: Storage Account Connection
# This example shows how to create a connection from AI Foundry to Azure Storage

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
  description = "Resource group name"
  type        = string
}

variable "storage_account_name" {
  description = "Name of the storage account"
  type        = string
}

# Reference existing resources
data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

data "azapi_resource" "ai_foundry" {
  type      = "Microsoft.CognitiveServices/accounts@2025-04-01-preview"
  name      = var.ai_foundry_name
  parent_id = data.azurerm_resource_group.rg.id
}

data "azurerm_storage_account" "storage" {
  name                = var.storage_account_name
  resource_group_name = var.resource_group_name
}

# Create Storage Account connection
resource "azapi_resource" "storage_connection" {
  type      = "Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview"
  name      = "${var.ai_foundry_name}-storage"
  parent_id = data.azapi_resource.ai_foundry.id

  body = {
    properties = {
      category      = "AzureBlob"
      target        = "https://${data.azurerm_storage_account.storage.name}.blob.core.windows.net/"
      authType      = "AccountManagedIdentity"
      isSharedToAll = true
      metadata = {
        ApiType    = "Azure"
        ResourceId = data.azurerm_storage_account.storage.id
        location   = data.azurerm_storage_account.storage.location
      }
    }
  }
}

output "connection_id" {
  value = azapi_resource.storage_connection.id
}
