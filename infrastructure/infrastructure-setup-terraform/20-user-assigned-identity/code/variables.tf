variable "location" {
  description = "The Azure region where resources will be deployed"
  type        = string
  default     = "eastus2"
}

variable "ai_foundry_name" {
  description = "The name of the AI Foundry account"
  type        = string
  default     = "foundry-uai"
}

variable "ai_project_name" {
  description = "The name of the AI Foundry project"
  type        = string
  default     = null # Will default to {ai_foundry_name}-proj
}

variable "create_user_assigned_identity" {
  description = "Whether to create a new User-Assigned Identity (true) or use an existing one (false)"
  type        = bool
  default     = true
}

variable "user_assigned_identity_name" {
  description = "The name of the User-Assigned Identity (for new or existing)"
  type        = string
  default     = "foundry-uai"
}

variable "user_assigned_identity_resource_group" {
  description = "The resource group of an existing User-Assigned Identity (only used if create_user_assigned_identity=false)"
  type        = string
  default     = null
}

variable "model_name" {
  description = "The model to deploy"
  type        = string
  default     = "gpt-4o"
}

variable "model_version" {
  description = "The version of the model"
  type        = string
  default     = "2024-08-06"
}

variable "model_capacity" {
  description = "The capacity (quota) for the model deployment"
  type        = number
  default     = 1
}
