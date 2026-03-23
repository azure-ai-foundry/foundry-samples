# Deploy Microsoft Foundry with Custom DNS

This Terraform template deploys an Microsoft Foundry resource with a custom DNS subdomain name.

## Description

- Creates an Microsoft Foundry account with a custom subdomain for API endpoints
- Creates a project
- Deploys a model

## Prerequisites

- Azure CLI or Terraform installed
- The custom subdomain name must be globally unique
- Appropriate Azure permissions

## Custom Subdomain

The custom subdomain defines the developer API endpoint subdomain. For example:
- Custom subdomain: `my-foundry-instance`
- API endpoint: `https://my-foundry-instance.cognitiveservices.azure.com/`

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Customize the `custom_subdomain_name` variable

4. Deploy:
```bash
terraform plan
terraform apply
```

## Resources Created

- Microsoft Foundry account (with custom subdomain)
- Microsoft Foundry project
- Model deployment

## Documentation

- [Custom subdomain names for Azure AI services](https://learn.microsoft.com/en-us/azure/ai-services/cognitive-services-custom-subdomains)
- [Microsoft Foundry overview](https://learn.microsoft.com/en-us/azure/ai-foundry/)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts, Custom DNS`
