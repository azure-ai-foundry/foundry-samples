---
description: This template demonstrates how to set up Microsoft Foundry with managed virtual network isolation using Terraform and AzAPI.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: managed-network-secured-agent
languages:
- hcl
---

# Microsoft Foundry: Standard Agent Setup with Managed Virtual Network (Terraform)

This folder provides a Terraform implementation for deploying Microsoft Foundry with managed virtual network isolation and private connectivity to supporting resources.

## Overview

This infrastructure-as-code (IaC) template provisions a Foundry account and project using preview APIs, then configures a managed network with outbound rules to private endpoints for supporting resources.

The template is designed for scenarios where you want:
- Managed virtual network isolation (Microsoft-managed network boundary)
- Foundry project setup for agent workloads
- Customer-managed storage/search/thread resources (BYO resources created by this template)
- Private endpoint + Private DNS based connectivity

## What This Template Deploys

Core resources:
- Resource group (name is suffixed with a random ID)
- Microsoft Foundry account (`Microsoft.CognitiveServices/accounts`, kind `AIServices`)
- Foundry project (`Microsoft.CognitiveServices/accounts/projects`)
- Managed network configuration on the Foundry account
- Managed network outbound rules for enabled backend resources

Optional backend resources (feature-flag driven):
- Azure Storage account
- Azure AI Search service
- Azure Cosmos DB for NoSQL account
- Virtual network and subnets
- Private endpoints
- Private DNS zones and VNet links
- Optional Windows VM + Bastion + Key Vault for jump-box access workflows

Project wiring and access setup:
- Project connections for Storage, AI Search, and Cosmos DB
- Project capability host (when Storage + Search + Cosmos are all enabled)
- Required RBAC role assignments for Foundry account and project identities

## Feature Flags

The deployment is controlled by boolean feature flags:

- `enable_networking`
- `enable_storage`
- `enable_aisearch`
- `enable_cosmos`
- `enable_vm`
- `enable_dns`

For a full managed-network private topology, set these to `true`:
- `enable_networking`
- `enable_storage`
- `enable_aisearch`
- `enable_cosmos`
- `enable_dns`

## Prerequisites

1. Active Azure subscription with permissions to create resources and assign RBAC roles.
1. Azure CLI authenticated to the target subscription.
1. Terraform CLI (1.x).
1. Access to preview API operations used by this template through AzAPI.
1. Resource providers registered in the target subscription:

```bash
az provider register --namespace "Microsoft.CognitiveServices"
az provider register --namespace "Microsoft.Network"
az provider register --namespace "Microsoft.Storage"
az provider register --namespace "Microsoft.Search"
az provider register --namespace "Microsoft.DocumentDB"
az provider register --namespace "Microsoft.KeyVault"
az provider register --namespace "Microsoft.ContainerService"
az provider register --namespace "Microsoft.App"
```

Recommended project-level post-deployment access:
- Assign developers the `Foundry User` role on the Foundry project scope.

## Variables

Required:

| Variable | Description |
|---|---|
| `subscription_id` | Azure subscription ID used by the `azurerm` provider |
| `resource_group_name` | Base resource group name (template appends a random suffix) |

Conditionally required:

| Variable | Required when | Description |
|---|---|---|
| `vm_admin_username` | `enable_vm = true` | Admin username for the optional Windows VM |

Common optional variables:

| Variable | Default | Description |
|---|---|---|
| `location` | `uaenorth` | Azure region for resource deployment |
| `foundry_identifier` | `foundry` | Prefix used to build Foundry account name |
| `tags` | `{}` | Tags applied to resources |
| `allowed_public_ips` | `[]` | Allowed CIDRs for Key Vault network ACLs (VM scenario) |
| `vnet_name` | `vnet-aifoundry` | VNet name |
| `vnet_address_prefix` | `10.0.0.0/16` | VNet CIDR |
| `private_endpoints_subnet_name` | `snet-privateendpoints` | Private endpoint subnet name |
| `private_endpoints_subnet_prefix` | `10.0.1.0/24` | Private endpoint subnet CIDR |
| `vm_subnet_name` | `snet-vms` | VM subnet name |
| `vm_subnet_prefix` | `10.0.2.0/24` | VM subnet CIDR |
| `bastion_subnet_prefix` | `10.0.3.0/26` | Azure Bastion subnet CIDR |
| `bastion_name` | `bastion-aifoundry` | Bastion name |
| `vm_name` | `vm-win2025` | Windows VM name |

Use [terraform.tfvars.example](terraform.tfvars.example) as a starting point.

## Deploy

1. Create a tfvars file from the example:

```bash
cp terraform.tfvars.example terraform.tfvars
```

2. Update at least:
- `subscription_id`
- `resource_group_name`
- feature flags for your scenario
- `vm_admin_username` if `enable_vm = true`

3. Initialize and deploy:

```bash
terraform init
terraform plan
terraform apply
```

## Important Behavior and Limitations

1. This template uses preview API versions via AzAPI for Foundry account/project/managed network resources.
1. The managed network is configured with:
   - `managedNetworkKind = "V2"`
   - `isolationMode = "AllowInternetOutbound"`
1. Project capability host creation is gated behind all three backend services being enabled:
   - `enable_storage = true`
   - `enable_aisearch = true`
   - `enable_cosmos = true`
1. Several explicit waits (`time_sleep`) are used to reduce transient failures from RBAC and outbound-rule propagation.
1. Resource names are suffixed with a random ID to reduce naming collisions.

## Outputs

The template exposes outputs for key resources, including:
- Resource group
- VNet/subnets (when enabled)
- Storage account (when enabled)
- Cosmos DB account (when enabled)
- AI Search service (when enabled)
- Foundry account name/ID/endpoint
- Private DNS zone IDs map (when DNS is enabled)
- VM/Bastion/Key Vault outputs (when VM is enabled)

See [outputs.tf](outputs.tf) for the full list.

## Cleanup

```bash
terraform destroy
```

If you plan to reuse network/subnet resources for another Foundry deployment, allow time for managed dependencies to fully unlink after deletion.

## References

- [Configure managed network in Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/)
- [AzAPI provider documentation](https://registry.terraform.io/providers/Azure/azapi/latest/docs)
- [AzureRM provider documentation](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)