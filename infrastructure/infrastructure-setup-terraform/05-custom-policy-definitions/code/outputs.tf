output "policy_definition_id" {
  description = "The ID of the deny disallowed connections policy definition"
  value       = azurerm_policy_definition.deny_disallowed_connections.id
}

output "policy_definition_name" {
  description = "The name of the deny disallowed connections policy definition"
  value       = azurerm_policy_definition.deny_disallowed_connections.name
}

output "policy_assignment_id" {
  description = "The ID of the policy assignment (if created)"
  value       = var.assign_policy ? azurerm_subscription_policy_assignment.deny_disallowed_connections[0].id : null
}

output "deny_key_auth_policy_id" {
  description = "The ID of the deny key auth connections policy definition"
  value       = azurerm_policy_definition.deny_key_auth_connections.id
}

output "deny_non_foundry_kinds_policy_id" {
  description = "The ID of the deny non-Foundry kinds policy definition"
  value       = azurerm_policy_definition.deny_non_foundry_kinds.id
}
