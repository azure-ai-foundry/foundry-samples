# Deploy Microsoft Foundry with Local Authentication Disabled

This Terraform template deploys an Microsoft Foundry resource and project with local (key-based) authentication disabled. Azure Entra ID authentication must be used instead.

## Description

- Creates an Microsoft Foundry account with `disableLocalAuth: true`
- Creates a project
- Deploys a GPT-4.1-mini model

## Prerequisites

- Azure CLI or Terraform installed
- Appropriate Azure permissions (Contributor role on subscription/resource group)

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Review and customize variables in `terraform.tfvars` or provide them via command line

4. Deploy:
```bash
terraform plan
terraform apply
```

## Important Notes

- With local auth disabled, you must use Azure Entra ID authentication
- Some Microsoft Foundry features work best with Entra ID authentication
- API keys will not work with this configuration

## Resources Created

- Microsoft Foundry account (Cognitive Services)
- Microsoft Foundry project
- Model deployment (GPT-4.1-mini)

## Documentation

- [Disable local authentication in Azure AI services](https://learn.microsoft.com/en-us/azure/ai-services/disable-local-auth)
- [Microsoft Foundry RBAC](https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/rbac-azure-ai-foundry)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts, Disable Local Auth, Entra ID`
