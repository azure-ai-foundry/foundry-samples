# Deploy AI Foundry with Basic Agent Setup and VNet Injection

This Terraform template deploys an AI Foundry resource with a basic agent configuration using VNet injection for network isolation. This is a "basic" agent setup — it does **not** create or connect BYO resources (Azure AI Search, Storage Account, Cosmos DB). Platform-managed resources are used instead.

## Description

- Creates a virtual network with an agent subnet (delegated to `Microsoft.App/environments`) and a private endpoint subnet
- Creates an AI Foundry account with VNet injection (network injection for agents)
- Creates a private endpoint and private DNS zones for the AI Services account
- Creates an AI Foundry project with system-assigned managed identity
- Creates a capability host for the project (basic agent, no BYO resources)
- Deploys a GPT-4o model
- Optionally creates an Azure Container Registry with a private endpoint

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ Virtual Network                                         │
│  ┌─────────────────────────┐  ┌──────────────────────┐ │
│  │ Agent Subnet            │  │ PE Subnet            │ │
│  │ (Microsoft.App/envs)    │  │  ┌─────────────────┐ │ │
│  │                         │  │  │ Private Endpoint│ │ │
│  │                         │  │  │ (AI Services)   │ │ │
│  │                         │  │  └─────────────────┘ │ │
│  │                         │  │  ┌─────────────────┐ │ │
│  │                         │  │  │ Private Endpoint│ │ │
│  │                         │  │  │ (ACR, optional) │ │ │
│  │                         │  │  └─────────────────┘ │ │
│  └─────────────────────────┘  └──────────────────────┘ │
└─────────────────────────────────────────────────────────┘
         │                              │
         ▼                              ▼
┌─────────────────────┐     ┌─────────────────────────┐
│ AI Foundry Account  │     │ Private DNS Zones        │
│  - GPT-4o model     │     │  - cognitiveservices     │
│  - Project          │     │  - services.ai           │
│  - Capability Host  │     │  - openai                │
│    (Basic Agent)    │     │  - azurecr.io (optional) │
└─────────────────────┘     └─────────────────────────┘
```

## Prerequisites

- Azure CLI and Terraform installed
- Appropriate Azure permissions (Contributor + User Access Administrator, or Owner)
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

3. Copy and customize variables:

```bash
cp example.tfvars terraform.tfvars
# Edit terraform.tfvars with your values
```

4. Deploy:

```bash
terraform plan -var-file=terraform.tfvars
terraform apply -var-file=terraform.tfvars
```

## Important Notes

- To access your Foundry resource securely, use a VM, VPN, or ExpressRoute connected to the VNet
- Public network access is completely disabled
- The agent subnet must use RFC1918 Class B or Class C address space
- The agent subnet is delegated to `Microsoft.App/environments` for VNet injection
- This is a **basic** agent setup — for standard agent setup with BYO resources, see template `15a`

## Resources Created

- Resource Group
- Virtual Network with two subnets (agent + private endpoint)
- AI Foundry account (with public network access disabled and VNet injection)
- Private Endpoint for AI Foundry
- Private DNS Zones (cognitiveservices, services.ai, openai)
- AI Foundry Project
- Capability Host (basic agent)
- Model Deployment (GPT-4o)
- Azure Container Registry with Private Endpoint (optional)

## Documentation

- [Configure private link for AI Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link)
- [Network isolation for agents](https://learn.microsoft.com/en-us/azure/ai-services/agents/concepts/networking)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts, Microsoft.Network/virtualNetworks, Microsoft.Network/privateEndpoints, Microsoft.ContainerRegistry/registries`
