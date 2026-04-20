---
description: This set of templates demonstrates how to set up a network-secured Azure AI Foundry environment for evaluation scenarios without Cosmos DB, AI Search, or project capability host.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: network-secured-evaluation-only
languages:
- bicep
- json
---

# Azure AI Foundry: Evaluation-Only Setup with Private Network Isolation

> **IMPORTANT**
> 
> This template is a simplified version of the [standard agent setup](../15-private-network-standard-agent-setup/) designed for **evaluation scenarios only**. It does **not** deploy Cosmos DB, AI Search, or a project capability host. If you need full agent capabilities (thread storage, vector search, stateful agents), use the standard agent setup instead.

---
## Overview
This infrastructure-as-code (IaC) solution deploys a **minimal** network-secured Azure AI Foundry environment with private networking and role-based access control (RBAC), intended for evaluation and testing purposes.

Unlike the full standard agent setup, this template:
- **Does NOT** create an Azure Cosmos DB account (no thread/conversation storage)
- **Does NOT** create an Azure AI Search resource (no vector stores)
- **Does NOT** create a project capability host (no stateful agent support)

What it **does** deploy:
- Azure AI Services account with a model deployment
- An AI Foundry project with a storage connection
- An Azure Storage account (or uses an existing one)
- A VNet with private endpoints for AI Services and Storage
- Private DNS zones for secure name resolution
- RBAC role assignments for the project on the storage account

---

## Key Information

**Region and Resource Placement Requirements**
- **All Foundry workspace resources should be in the same region as the VNet**, including the Storage Account, Foundry Account, Project, and Managed Identity. The only exception is within the Foundry Account, you may choose to deploy your model to a different region.
  - **Note:** Your Virtual Network can be in a different resource group than your Foundry workspace resources.

---

## Prerequisites

1. **Active Azure subscription with appropriate permissions**
   - **Azure AI Account Owner**: Needed to create a cognitive services account and project 
   - **Owner or Role Based Access Administrator**: Needed to assign RBAC to the storage account
   - **Azure AI User**: Needed to create and use evaluation workloads

1. **Register Resource Providers**

   ```bash
   az provider register --namespace 'Microsoft.KeyVault'
   az provider register --namespace 'Microsoft.CognitiveServices'
   az provider register --namespace 'Microsoft.Storage'
   az provider register --namespace 'Microsoft.Network'
   az provider register --namespace 'Microsoft.App'
   az provider register --namespace 'Microsoft.ContainerService'
   ```

1. Network administrator permissions (if operating in a restricted or enterprise environment)

1. Sufficient quota for all resources in your target Azure region
    * If no parameters are passed in, this template creates an Azure AI Foundry resource, Foundry project, and Azure Storage account

1. Azure CLI installed and configured on your local workstation or deployment pipeline server

---

## Pre-Deployment Steps

### Networking Requirements
1. Review network requirements and plan Virtual Network address space (e.g., 192.168.0.0/16)

2. Two subnets are needed:  
    - **Agent Subnet** (e.g., 192.168.0.0/24): Hosts Agent client for workloads, delegated to Microsoft.App/environments
    - **Private endpoint Subnet** (e.g., 192.168.1.0/24): Hosts private endpoints 
    - Ensure that the address spaces do not overlap with any existing networks
  
  > **Notes:** 
  - If you do not provide an existing virtual network, the template will create a new virtual network with the default address spaces and subnets described above.
  - You must ensure the subnet is not already in use by another account.
  - You must ensure the subnet is exclusively delegated to __Microsoft.App/environments__.

---

## Template Customization

Note: If not provided, the following resources will be created automatically for you:
- VNet and two subnets
- Azure Storage

### Parameters

1. **Use Existing Virtual Network and Subnets**

To use an existing VNet and subnets, set the `existingVnetResourceId` parameter to the full Azure Resource ID of the target VNet:
```
param existingVnetResourceId = "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/virtualNetworks/<vnet-name>"
param agentSubnetName string = 'agent-subnet'
param peSubnetName string = 'pe-subnet'
```

2. **Use an existing Azure Storage account**

To use an existing Azure Storage account:
```
param azureStorageAccountResourceId string = /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}
```

---

## Deploy the bicep template

**Option 1: Manually deploy the bicep template**
- **Create a New (or Use Existing) Resource Group**

   ```bash
   az group create --name <new-rg-name> --location <your-rg-region>
   ```
- Deploy the main.bicep file

   ```bash
   az deployment group create --resource-group <your-resource-group> --template-file main.bicep --parameters main.bicepparam
   ```

> **Note:** To access your Foundry resource securely, use either a VM, VPN, or ExpressRoute.

---  

## Architecture

### Azure Resources Created

| Resource | Type | Description |
|----------|------|-------------|
| Azure AI Foundry | `Microsoft.CognitiveServices/accounts` | AI Services account with disabled public access |
| AI Model Deployment | `Microsoft.CognitiveServices/accounts/deployments` | Model deployment (e.g., gpt-4.1) |
| Foundry Project | `Microsoft.CognitiveServices/accounts/projects` | Project with system-assigned managed identity |
| Storage Account | `Microsoft.Storage/storageAccounts` | StorageV2 with disabled public access |
| Virtual Network | `Microsoft.Network/virtualNetworks` | VNet with agent and PE subnets |
| Private Endpoints | `Microsoft.Network/privateEndpoints` | For AI Services and Storage |

### Network Security Design

**Private Endpoints** are created for:
- Azure AI Foundry (account)
- Azure Storage (blob)

**Private DNS Zones**:
| Private Link Resource Type | Sub Resource | Private DNS Zone Name |
|----------------------------|--------------|------------------------|
| **Azure AI Foundry** | account | `privatelink.cognitiveservices.azure.com`<br>`privatelink.openai.azure.com`<br>`privatelink.services.ai.azure.com` |
| **Azure Storage** | blob | `privatelink.blob.core.windows.net` |

### Role Assignments

- **AI Services Account**
  - Azure AI User (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) — grants the project MI data-plane access
- **Azure Storage Account**
  - Storage Blob Data Contributor (`ba92f5b4-2d11-453d-a403-e96b0029c9fe`)
  - Storage Blob Data Owner (`b7e6dc6d-f1e8-4753-8033-0f276bb0955b`) — scoped to project containers

---

## Module Structure

```text
modules-network-secured/
├── ai-account-identity.bicep                       # Azure AI Foundry deployment and configuration
├── ai-account-role-assignment.bicep                # Azure AI User role assignment on the account
├── ai-project-identity.bicep                       # Foundry project deployment with storage connection
├── azure-storage-account-role-assignment.bicep      # Storage Account RBAC configuration
├── blob-storage-container-role-assignments.bicep    # Blob Storage Container RBAC configuration
├── existing-vnet.bicep                             # Bring your existing virtual network
├── format-project-workspace-id.bicep               # Formatting the project workspace ID
├── network-agent-vnet.bicep                        # Logic for routing virtual network set-up
├── private-endpoint-and-dns.bicep                  # Private endpoints and DNS zones (AI Services + Storage only)
├── standard-dependent-resources.bicep              # Deploying Storage Account
├── subnet.bicep                                    # Setting the subnet
├── validate-existing-resources.bicep               # Validate existing Storage Account
└── vnet.bicep                                      # Deploying a new virtual network
```

---

## Comparison with Standard Agent Setup

| Feature | This Template (Evaluation-Only) | Standard Agent Setup (15) |
|---------|-------------------------------|--------------------------|
| AI Services + Model | ✅ | ✅ |
| Project | ✅ | ✅ |
| Storage Account | ✅ | ✅ |
| VNet + Private Endpoints | ✅ (AI + Storage) | ✅ (AI + Storage + Search + Cosmos) |
| Cosmos DB | ❌ | ✅ |
| AI Search | ❌ | ✅ |
| Project Capability Host | ❌ | ✅ |
| Stateful Agents | ❌ | ✅ |

---

## Maintenance

### Troubleshooting

1. Verify private endpoint connectivity
2. Check DNS resolution
3. Validate role assignments
4. Review network security groups
