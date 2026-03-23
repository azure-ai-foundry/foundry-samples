# Microsoft Foundry Agent Service: Basic Agent Setup with the Bing Search Tool

Use this template as a starting point for creating a basic agent project where you know you will want to create agents with the Grounding with Bing Search tool.

For more information, see [Azure AI Services Agents Environment Setup](https://learn.microsoft.com/en-us/azure/ai-services/agents/environment-setup).

> **Note:** Deploying the template does not create an agent; it only provisions the necessary resources to get started.

## Description

- Creates an Microsoft Foundry account
- Creates a project
- Deploys a GPT-4.1 model
- Creates a Bing Grounding connection with API key authentication

## Prerequisites

- Azure CLI or Terraform installed
- A valid Bing Search v7 API key (obtain from Azure Portal)
- Appropriate Azure permissions

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Provide your Bing API key via terraform.tfvars or environment variable

4. Deploy:
```bash
terraform plan
terraform apply
```

## Getting Started

Creating your first agent with Microsoft Foundry Agent Service is a two-step process:

1. **Set up your agent environment** (this template)
2. **Create and configure your agent** with Bing Search tool using your preferred SDK or the Azure Foundry Portal

## Resources Created

- Microsoft Foundry account
- Microsoft Foundry project  
- Model deployment (GPT-4.1)
- Bing Grounding connection

## Documentation

- [Set up your agent environment](https://learn.microsoft.com/en-us/azure/ai-services/agents/environment-setup)
- [Bing Search API](https://learn.microsoft.com/en-us/bing/search-apis/bing-web-search/overview)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts/projects, Microsoft.CognitiveServices/accounts/connections, Bing Search`
