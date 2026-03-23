variable "policy_name" {
  description = "Name for the custom policy definition"
  type        = string
  default     = "deny-disallowed-foundry-connections"
}

variable "allowed_categories" {
  description = "List of allowed connection categories for AI Foundry"
  type        = list(string)
  default     = ["BingLLMSearch"]
}

variable "assign_policy" {
  description = "Whether to assign the policy to the subscription"
  type        = bool
  default     = false
}

variable "assignment_name" {
  description = "Name for the policy assignment (if enabled)"
  type        = string
  default     = "deny-disallowed-connections-assignment"
}

variable "assignment_display_name" {
  description = "Display name for the policy assignment"
  type        = string
  default     = "Deny Disallowed Foundry Connections"
}

variable "allowed_mcp_sources" {
  description = "List of allowed MCP connection target addresses"
  type        = list(string)
  default     = ["https://api.githubcopilot.com/mcp"]
}
