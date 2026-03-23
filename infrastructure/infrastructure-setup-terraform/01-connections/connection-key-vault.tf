# Example: Key Vault Connection
# This example shows how to create a connection from AI Foundry to Azure Key Vault
# using Managed Identity authentication

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
  description = "Resource group containing AI Foundry and Key Vault"
  type        = string
}

variable "key_vault_name" {
  description = "Name of the Key Vault"
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

data "azurerm_key_vault" "kv" {
  name                = var.key_vault_name
  resource_group_name = var.resource_group_name
}

# Create Key Vault connection
resource "azapi_resource" "key_vault_connection" {
  type      = "Microsoft.CognitiveServices/accounts/connections@2025-04-01-preview"
  name      = "${var.ai_foundry_name}-keyvault"
  parent_id = data.azapi_resource.ai_foundry.id

  body = {
    properties = {
      category      = "AzureKeyVault"
      target        = data.azurerm_key_vault.kv.id
      authType      = "AccountManagedIdentity"
      isSharedToAll = true
      metadata = {
        ApiType    = "Azure"
        ResourceId = data.azurerm_key_vault.kv.id
        location   = data.azurerm_key_vault.kv.location
      }
    }
  }
}

# Grant AI Foundry managed identity access to Key Vault
# The AI Foundry account must have a system-assigned managed identity
resource "azurerm_role_assignment" "kv_secrets_officer" {
  scope                = data.azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = jsondecode(data.azapi_resource.ai_foundry.output).identity.principalId
}

output "connection_id" {
  value = azapi_resource.key_vault_connection.id
}

# NOTE: All subsequent connections should depend on both the key vault connection
# and the role assignment for proper sequencing
