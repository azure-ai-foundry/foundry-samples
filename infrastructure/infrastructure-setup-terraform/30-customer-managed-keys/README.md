# Microsoft Foundry with Customer Managed Keys (CMK)

This Terraform template deploys Microsoft Foundry with Customer-Managed Key (CMK) encryption for data at rest.

## Description

- Creates an Microsoft Foundry account
- Configures Customer-Managed Key encryption using Azure Key Vault
- Creates a project
- Deploys a model

## Important Notes

- Agent APIs do not support customer-managed key encryption in basic setup
- To use CMK with Agents, you must bring your own storage resources using 'standard' agent setup (see example 31)
- Due to role assignment propagation delays, the initial deployment may fail if the managed identity doesn't have Key Vault access yet - retry if this occurs

## Prerequisites

- Azure CLI or Terraform installed
- An existing Azure Key Vault with a key
- Appropriate Azure permissions (Key Vault Administrator or Contributor)

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Configure variables with your Key Vault details:
   - Key Vault name
   - Key name
   - Key version

4. Deploy:
```bash
terraform plan
terraform apply
```

## Resources Created

- Microsoft Foundry account (with CMK encryption)
- Microsoft Foundry project
- Model deployment
- Role assignments for Key Vault access

## AzAPI Usage

Uses AzAPI for:
- Customer-managed key configuration on Microsoft Foundry account (not yet available in AzureRM provider)

## Documentation

- [Encrypt data at rest with customer-managed keys](https://learn.microsoft.com/en-us/azure/ai-services/encrypt-data-at-rest)
- [azurerm_key_vault - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/key_vault)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts, Customer Managed Keys, Key Vault`
