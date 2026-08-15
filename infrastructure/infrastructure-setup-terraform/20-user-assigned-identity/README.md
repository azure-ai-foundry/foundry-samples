# Set up Microsoft Foundry with User-Assigned Managed Identity

This Terraform template deploys a Microsoft Foundry account and project configured with a User-Assigned Managed Identity instead of the default System-Assigned identity.

## Description

- Creates a Microsoft Foundry account with User-Assigned Managed Identity
- Creates a project with User-Assigned Managed Identity
- Optionally deploys a GPT-4o model
- Creates a Log Analytics workspace
- Creates a workspace-based Application Insights component
- Creates an Application Insights connection on the project
- Assigns monitoring roles to the User-Assigned Managed Identity

By default, the project uses the Application Insights connection string to authenticate trace ingestion. The template assigns Log Analytics Reader and Privileged Monitoring Data Reader to the identity so the project can query traces, including generative AI content.

To use the project managed identity for trace ingestion instead, set `use_project_managed_identity_for_trace_ingestion = true`. The template then disables local Application Insights authentication and assigns the identity the Monitoring Metrics Publisher role. Managed-identity connections remain project-scoped because the service does not support sharing them with all project users.

## Prerequisites

- Azure CLI or Terraform installed
- An existing User-Assigned Managed Identity (or this template can create one)
- Permission to create role assignments at the Application Insights resource scope

## Limitations

- When creating a project, managed identity type cannot be updated later
- User-Assigned Managed Identity is not supported with Customer Managed Keys
- A project can have only one Application Insights connection

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
   - Set `deploy_model = true` to deploy the configured model
   - Set `use_project_managed_identity_for_trace_ingestion = true` to use identity-based trace ingestion

4. Deploy:
```bash
terraform plan
terraform apply
```

## Resources Created

- User-Assigned Managed Identity (optional, if not provided)
- Microsoft Foundry account with UAI
- Microsoft Foundry project with UAI
- Log Analytics workspace
- Application Insights component and project connection
- Application Insights monitoring role assignments
- Model deployment (optional)

## Documentation

- [Managed identities for Azure resources](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)
- [azurerm_user_assigned_identity - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/user_assigned_identity)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts/projects, Microsoft.ManagedIdentity/userAssignedIdentities`
