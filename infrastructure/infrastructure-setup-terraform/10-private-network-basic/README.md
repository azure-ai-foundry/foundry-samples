# Deploy Microsoft Foundry with Private Network Configuration

This Terraform template deploys an Microsoft Foundry resource with public network access disabled and a private endpoint for secure access.

## Description

- Creates an Microsoft Foundry account with `publicNetworkAccess: Disabled`
- Creates a virtual network and subnet for private endpoints
- Creates a private endpoint to access the Microsoft Foundry resource
- Creates a project
- Deploys a GPT-4o model

## Prerequisites

- Azure CLI or Terraform installed
- Appropriate Azure permissions
- Access to the VNet (VM, VPN, or ExpressRoute) to use the private Foundry resource

## Deployment

1. Navigate to the code directory:
```bash
cd code
```

2. Initialize Terraform:
```bash
terraform init
```

3. Review and customize variables

4. Deploy:
```bash
terraform plan
terraform apply
```

## Important Notes

- To access your Foundry resource securely, use a VM, VPN, or ExpressRoute connected to the VNet
- Public network access is completely disabled
- Private DNS zone integration may be needed for name resolution

## Resources Created

- Virtual Network and Subnet
- Microsoft Foundry account (with public network access disabled)
- Private Endpoint
- Microsoft Foundry project
- Model deployment

## Documentation

- [Configure private link for Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link)
- [azurerm_private_endpoint - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/private_endpoint)
- [azurerm_private_dns_zone - Terraform](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/private_dns_zone)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts, Microsoft.Network/virtualNetworks, Microsoft.Network/privateEndpoints`
