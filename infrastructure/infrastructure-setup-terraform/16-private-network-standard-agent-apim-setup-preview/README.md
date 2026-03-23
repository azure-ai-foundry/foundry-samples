# Private Network Standard Agent with API Management Setup (Preview)

This folder provides a Terraform implementation for private network standard agent setup with Azure API Management integration.

## Overview

This advanced scenario combines:
- Private network configuration with VNet injection
- Standard agent setup with BYOS (bring-your-own-storage/search)
- Azure API Management integration for controlled access
- Private endpoints for all services

## Status

**🚧 PARTIALLY IMPLEMENTED** - This is a complex scenario requiring:

1. Private networking infrastructure (VNets, subnets, private endpoints)
2. Standard agent setup with Storage, Search, and Cosmos DB
3. Azure API Management configuration
4. Private DNS zones and network integration
5. Complex RBAC and connection setup

## Reference

For implementation guidance, see the Bicep reference:
- `infrastructure-setup-bicep/16-private-network-standard-agent-apim-setup-preview`

For a working private network example, see:
- `../15a-private-network-standard-agent-setup`

## Prerequisites

- Advanced networking knowledge
- API Management experience
- Understanding of private endpoints and DNS
- Familiarity with standard agent setup

## AzAPI Usage Rationale

This scenario requires extensive use of AzAPI provider for:
- Microsoft Foundry account and project configuration
- Connection resources (not yet in AzureRM)
- Advanced networking configurations

## Contributing

If you implement this scenario, please:
1. Follow the existing Terraform patterns in this repository
2. Use AzAPI for unsupported resources
3. Include comprehensive README documentation
4. Add example.tfvars with all required variables

## Documentation

- [Configure private link for Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link)
- [Azure API Management with virtual networks](https://learn.microsoft.com/en-us/azure/api-management/api-management-using-with-vnet)
- [azurerm_api_management - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/api_management)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Private Network, APIM, Standard Agent, Advanced, Preview`
