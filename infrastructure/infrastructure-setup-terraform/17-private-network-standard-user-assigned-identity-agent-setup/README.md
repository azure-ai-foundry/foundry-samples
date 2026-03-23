# Private Network Standard Agent with User-Assigned Identity Setup

This folder provides a Terraform implementation for private network standard agent setup with User-Assigned Managed Identity.

## Overview

This scenario combines:
- Private network configuration (VNet, private endpoints)
- Standard agent setup (BYOS)
- User-Assigned Managed Identity instead of System-Assigned
- Private DNS configuration

## Status

**🚧 PARTIALLY IMPLEMENTED** - This scenario requires:

1. User-Assigned Managed Identity creation
2. Private networking infrastructure
3. Standard agent resources (Storage, Search, Cosmos DB)
4. RBAC assignments for UAI
5. Private endpoint configuration
6. Connection resources using UAI authentication

## Reference

For implementation guidance, see:
- Bicep: `infrastructure-setup-bicep/17-private-network-standard-user-assigned-identity-agent-setup`
- Similar Terraform: `../15a-private-network-standard-agent-setup` (uses system-assigned identity)
- UAI example: `../20-user-assigned-identity` (without private networking)

## Key Differences from System-Assigned

- UAI must be created before Microsoft Foundry account
- All resources must reference the UAI resource ID
- RBAC assignments use the UAI principal ID
- UAI can be shared across multiple resources

## AzAPI Usage Rationale

Requires AzAPI for:
- Microsoft Foundry account configuration with UAI
- Project configuration with UAI
- Connection resources

## Prerequisites

- Understanding of Azure Managed Identities
- Private networking knowledge
- Standard agent setup familiarity

## Documentation

- [Configure private link for Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link)
- [Managed identities for Azure resources](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)
- [azurerm_user_assigned_identity - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/user_assigned_identity)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Private Network, User-Assigned Identity, Standard Agent`
