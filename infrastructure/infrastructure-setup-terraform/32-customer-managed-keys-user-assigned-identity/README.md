# Customer Managed Keys with User-Assigned Identity

This folder provides a Terraform implementation for CMK encryption with User-Assigned Managed Identity (with noted platform limitations).

## Overview

Combines:
- Customer-Managed Key (CMK) encryption
- User-Assigned Managed Identity
- Key Vault integration
- Microsoft Foundry and project configuration

## Status

**🚧 PARTIALLY IMPLEMENTED** - This scenario requires:

1. User-Assigned Managed Identity creation
2. Key Vault with encryption key
3. RBAC assignments for UAI to Key Vault
4. Microsoft Foundry with CMK and UAI
5. Project with UAI

## Important Limitation

**User-Assigned Managed Identity is NOT supported with Customer Managed Keys** in basic setup.

For CMK with agents, you must use:
- System-Assigned Identity + Standard Agent Setup (see example 31)

## Reference

For guidance, see the Bicep reference:
- `infrastructure-setup-bicep/32-customer-managed-keys-user-assigned-identity`

This scenario may have limited applicability due to the UAI+CMK restriction.

## Prerequisites

- Understanding of Azure Managed Identities
- Key Vault and encryption knowledge
- Awareness of CMK limitations

## Alternative Approaches

Consider these alternatives:
- Use System-Assigned Identity with CMK (example 30)
- Use UAI without CMK (example 20)
- Use Standard Agent Setup with CMK (example 31)

## Documentation

- [Encrypt data at rest with customer-managed keys](https://learn.microsoft.com/en-us/azure/ai-services/encrypt-data-at-rest)
- [azurerm_key_vault - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/key_vault)
- [azurerm_user_assigned_identity - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/user_assigned_identity)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Customer Managed Keys, User-Assigned Identity, Encryption`
