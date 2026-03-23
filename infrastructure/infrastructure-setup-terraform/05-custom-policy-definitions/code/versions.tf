# Configure the AzureRM provider
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.37"
    }
  }
  required_version = ">= 1.10.0, < 2.0.0"
}
