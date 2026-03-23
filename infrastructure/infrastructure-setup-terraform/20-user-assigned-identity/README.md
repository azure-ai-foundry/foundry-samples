# Set up Microsoft Foundry with User-Assigned Managed Identity

This Terraform template deploys an Microsoft Foundry account and project configured with a User-Assigned Managed Identity instead of the default System-Assigned identity.

## Description

- Creates an Microsoft Foundry account with User-Assigned Managed Identity
- Creates a project with User-Assigned Managed Identity
- Deploys a GPT-4o model

## Prerequisites

- Azure CLI or Terraform installed
- An existing User-Assigned Managed Identity (or this template can create one)
- Appropriate Azure permissions

## Limitations

- When creating a project, managed identity type cannot be updated later
- User-Assigned Managed Identity is not supported with Customer Managed Keys

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Configure variables (either in terraform.tfvars or via command line):
   - Provide existing UAI name and resource group, OR
   - Set `create_user_assigned_identity = true` to create a new one

4. Deploy:
```bash
terraform plan
terraform apply
```

## Resources Created

- User-Assigned Managed Identity (optional, if not provided)
- Microsoft Foundry account with UAI
- Microsoft Foundry project with UAI
- Model deployment (GPT-4o)

## Documentation

- [Managed identities for Azure resources](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)
- [azurerm_user_assigned_identity - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/user_assigned_identity)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts/projects, Microsoft.ManagedIdentity/userAssignedIdentities`
