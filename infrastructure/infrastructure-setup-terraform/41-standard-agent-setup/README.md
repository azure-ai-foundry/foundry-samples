# Microsoft Foundry Agent Service: Standard Agent Setup

This Terraform template provisions resources for standard agent setup with bring-your-own search and storage (BYOS).

## Description

- Creates a Cognitive Services Account
- Deploys a GPT-4.1 model
- Creates a project
- **Creates Azure Storage Account for agent data**
- **Creates Azure AI Search for agent indexing**
- **Creates Cosmos DB for agent threads**
- Connects these resources to the project

## Standard vs Basic Setup

**Standard Setup** (this template):
- You manage and control the storage, search, and database resources
- Better for production workloads requiring specific configurations
- Supports customer-managed keys and private networking
- More control over data residency and compliance

**Basic Setup** (see example 40):
- Microsoft manages storage and search resources
- Simpler setup for development and testing

## Prerequisites

- Azure CLI or Terraform installed
- Appropriate Azure permissions (Contributor role)

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Customize variables in terraform.tfvars

4. Deploy:
```bash
terraform plan
terraform apply
```

## Resources Created

- Microsoft Foundry account
- Microsoft Foundry project
- GPT-4.1 model deployment
- Storage Account (for agent file storage)
- Azure AI Search (for agent indexing)
- Cosmos DB Account (for agent threads)
- Connections and RBAC assignments

## Documentation

- [Set up your agent environment](https://learn.microsoft.com/en-us/azure/ai-services/agents/environment-setup)
- [azurerm_storage_account - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/storage_account)
- [azurerm_search_service - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/search_service)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts/projects, Standard Agent, BYOS`
