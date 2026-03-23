# Entra ID Passthrough Authentication with Storage Connection

This Terraform template sets up a connection to a storage account and assigns Entra ID passthrough for your storage resource.

## Description

- Creates a Storage Account with public network access disabled and shared key access disabled
- Creates a Microsoft Foundry account with public network access disabled
- Creates a project with an Azure Storage connection using Entra ID (AAD) passthrough authentication
- Assigns Storage Blob Data Owner role to the project managed identity on the storage account

## Entra ID Passthrough

The storage connection uses Entra ID (AAD) authentication instead of API keys, providing:
- Enhanced security with identity-based access
- Audit trails for user actions
- No API key management required

## Prerequisites

- Azure CLI or Terraform installed
- Entra ID tenant access
- Appropriate permissions

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Deploy:
```bash
terraform plan
terraform apply
```

## Resources Created

- Storage Account (private network, no shared key access)
- Microsoft Foundry account (private network)
- Microsoft Foundry project
- Azure Storage connection with Entra ID auth
- Storage Blob Data Owner role assignment

## Documentation

- [Microsoft Foundry RBAC](https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/rbac-azure-ai-foundry)
- [Disable local authentication in Azure AI services](https://learn.microsoft.com/en-us/azure/ai-services/disable-local-auth)
- [Azure Storage Account - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/storage_account)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts, Entra ID, Authentication, Storage`
