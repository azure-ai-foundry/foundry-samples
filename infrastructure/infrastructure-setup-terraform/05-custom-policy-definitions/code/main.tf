########## Create Azure Policy Definitions
##########

## Get subscription data
data "azurerm_client_config" "current" {}
data "azurerm_subscription" "current" {}

## Policy Definition: Deny Disallowed Connection Categories
resource "azurerm_policy_definition" "deny_disallowed_connections" {
  name         = var.policy_name
  policy_type  = "Custom"
  mode         = "All"
  display_name = "Only selected Foundry connection categories are allowed"
  description  = "Only selected Foundry connection categories are allowed"

  metadata = jsonencode({
    version = "1.0.0"
  })

  parameters = jsonencode({
    allowedCategories = {
      type = "Array"
      metadata = {
        description = "Categories allowed for Microsoft.CognitiveServices/accounts/connections and Microsoft.CognitiveServices/accounts/projects/connections"
        displayName = "Allowed connection categories"
      }
      defaultValue = var.allowed_categories
    }
  })

  policy_rule = jsonencode({
    if = {
      anyOf = [
        {
          allOf = [
            {
              field  = "type"
              equals = "Microsoft.CognitiveServices/accounts/connections"
            },
            {
              field = "Microsoft.CognitiveServices/accounts/connections/category"
              notIn = "[parameters('allowedCategories')]"
            }
          ]
        },
        {
          allOf = [
            {
              field  = "type"
              equals = "Microsoft.CognitiveServices/accounts/projects/connections"
            },
            {
              field = "Microsoft.CognitiveServices/accounts/projects/connections/category"
              notIn = "[parameters('allowedCategories')]"
            }
          ]
        }
      ]
    }
    then = {
      effect = "deny"
    }
  })
}

## Policy Assignment (optional)
resource "azurerm_subscription_policy_assignment" "deny_disallowed_connections" {
  count                = var.assign_policy ? 1 : 0
  name                 = var.assignment_name
  display_name         = var.assignment_display_name
  policy_definition_id = azurerm_policy_definition.deny_disallowed_connections.id
  subscription_id      = data.azurerm_subscription.current.id

  parameters = jsonencode({
    allowedCategories = {
      value = var.allowed_categories
    }
  })
}

## Additional Policy: Deny Key-Based Authentication Connections
resource "azurerm_policy_definition" "deny_key_auth_connections" {
  name         = "deny-key-auth-connections"
  policy_type  = "Custom"
  mode         = "All"
  display_name = "Deny AI Foundry connections using API key-based authentication"
  description  = "This policy denies the creation of connections that use API key authentication for enhanced security"

  metadata = jsonencode({
    version = "1.0.0"
  })

  policy_rule = jsonencode({
    if = {
      anyOf = [
        {
          allOf = [
            {
              field  = "type"
              equals = "Microsoft.CognitiveServices/accounts/projects/connections"
            },
            {
              field  = "Microsoft.CognitiveServices/accounts/projects/connections/authType"
              equals = "ApiKey"
            }
          ]
        },
        {
          allOf = [
            {
              field  = "type"
              equals = "Microsoft.CognitiveServices/accounts/connections"
            },
            {
              field  = "Microsoft.CognitiveServices/accounts/connections/authType"
              equals = "ApiKey"
            }
          ]
        }
      ]
    }
    then = {
      effect = "deny"
    }
  })
}

## Policy: Deny Disallowed MCP Tools
resource "azurerm_policy_definition" "deny_disallowed_mcp_tools" {
  name         = "deny-disallowed-mcp-tools"
  policy_type  = "Custom"
  mode         = "All"
  display_name = "Only allow Foundry MCP connections from select sources"
  description  = "Only selected Foundry MCP connection sources are allowed"

  metadata = jsonencode({
    version = "1.0.0"
  })

  parameters = jsonencode({
    allowedSources = {
      type = "Array"
      metadata = {
        description = "Only select target addresses are allowed for MCP connections."
        displayName = "Allowed connection targets"
      }
      defaultValue = var.allowed_mcp_sources
    }
  })

  policy_rule = jsonencode({
    if = {
      anyOf = [
        {
          allOf = [
            {
              field  = "type"
              equals = "Microsoft.CognitiveServices/accounts/connections"
            },
            {
              field  = "Microsoft.CognitiveServices/accounts/connections/category"
              equals = "RemoteTool"
            },
            {
              field = "Microsoft.CognitiveServices/accounts/connections/target"
              notIn = "[parameters('allowedSources')]"
            }
          ]
        },
        {
          allOf = [
            {
              field  = "type"
              equals = "Microsoft.CognitiveServices/accounts/projects/connections"
            },
            {
              field  = "Microsoft.CognitiveServices/accounts/connections/category"
              equals = "RemoteTool"
            },
            {
              field = "Microsoft.CognitiveServices/accounts/projects/connections/target"
              notIn = "[parameters('allowedSources')]"
            }
          ]
        }
      ]
    }
    then = {
      effect = "deny"
    }
  })
}

## Additional Policy: Deny Non-AIServices Resource Kinds
resource "azurerm_policy_definition" "deny_non_foundry_kinds" {
  name         = "deny-non-foundry-resource-kinds"
  policy_type  = "Custom"
  mode         = "All"
  display_name = "Deny account kinds that do not support the full AI Foundry capabilities."
  description  = "This policy denies the creation of account kinds that do not support the full AI Foundry capabilities."

  metadata = jsonencode({
    version = "1.0.0"
  })

  policy_rule = jsonencode({
    if = {
      allOf = [
        {
          field  = "type"
          equals = "Microsoft.CognitiveServices/accounts"
        },
        {
          field     = "kind"
          notEquals = "AIServices"
        }
      ]
    }
    then = {
      effect = "deny"
    }
  })
}
