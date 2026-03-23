# Customer Managed Keys with Standard Agent Setup

This folder provides a Terraform implementation for CMK encryption with standard agent setup (BYOS).

## Overview

Combines Customer-Managed Key (CMK) encryption with standard agent setup:
- Microsoft Foundry with CMK encryption
- Standard agent resources (Storage, Search, Cosmos DB) with CMK
- Key Vault integration
- Proper key rotation support

## Status

**🚧 PARTIALLY IMPLEMENTED** - This scenario requires:

1. Key Vault with keys for all resources
2. Microsoft Foundry account with CMK
3. Storage Account with CMK
4. Cosmos DB with CMK (if supported)
5. Proper RBAC for key access
6. Role propagation handling

## Important Notes

- Agent APIs **require** standard setup (BYOS) for CMK support
- All encryption keys can be in the same Key Vault or separate
- Consider key rotation policies
- Monitor key access patterns

## Reference

For guidance, see:
- Bicep: `infrastructure-setup-bicep/31-customer-managed-keys-standard-agent`
- CMK without agents: `../30-customer-managed-keys`
- Standard agent: `../41-standard-agent-setup`

## Prerequisites

- Key Vault with keys created
- Cryptographic permissions in Key Vault
- Understanding of Azure encryption
- Knowledge of key rotation

## AzAPI Usage Rationale

Uses AzAPI for:
- CMK configuration on Microsoft Foundry (not in AzureRM)
- Advanced encryption settings
- Key Vault property updates

## Documentation

- [Encrypt data at rest with customer-managed keys](https://learn.microsoft.com/en-us/azure/ai-services/encrypt-data-at-rest)
- [Set up your agent environment](https://learn.microsoft.com/en-us/azure/ai-services/agents/environment-setup)
- [azurerm_key_vault - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/key_vault)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Customer Managed Keys, Standard Agent, Encryption, BYOS`
