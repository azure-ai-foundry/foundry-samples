# 46 - Standard Agent Setup with BYO VNet and GSA Proxy

This sample deploys an Azure AI Foundry agent setup with:

- **BYO Virtual Network** with agent subnet delegation (no private endpoints)
- **GSA AI Connector proxy** VM for egress traffic control
- **UDR (User Defined Route)** that routes `0.0.0.0/0` traffic from the agent subnet through the GSA proxy
- **Service tag exceptions** in the UDR so critical Azure traffic bypasses the proxy

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  Virtual Network (10.0.0.0/16)                                  │
│                                                                 │
│  ┌───────────────────────────┐  ┌─────────────────────────────┐ │
│  │ agent-subnet (10.0.0.0/24)│  │ gsa-proxy-subnet            │ │
│  │                           │  │ (10.0.1.0/24)               │ │
│  │  Delegated to             │  │                             │ │
│  │  Microsoft.App/environments│  │  ┌──────────────────────┐  │ │
│  │                           │  │  │ GSA Proxy VM          │  │ │
│  │  UDR: 0/0 → Proxy IP     │  │  │ - Managed Identity    │  │ │
│  │  (with service tag        │──│──│ - IP Forwarding: ON   │  │ │
│  │   exceptions)             │  │  │ - NSG: VNet allowed   │  │ │
│  │                           │  │  └──────────────────────┘  │ │
│  └───────────────────────────┘  └─────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
         │
         ├── AI Services (publicNetworkAccess: Enabled)
         │     └── networkInjections: agent subnet
         ├── AI Project
         ├── Cosmos DB
         ├── AI Search
         └── Storage Account
```

## Key Differences from Other Setups

| Feature | 41-standard-agent | 15-private-network | **46-byovnet-gsa-proxy** |
|---|---|---|---|
| VNet | ❌ | ✅ | ✅ |
| Private Endpoints | ❌ | ✅ | ❌ |
| DNS Zones | ❌ | ✅ | ❌ |
| Public Network Access | Enabled | Disabled | **Enabled** |
| Agent Subnet Delegation | ❌ | ✅ | ✅ |
| GSA Proxy | ❌ | ❌ | ✅ |
| UDR (egress control) | ❌ | ❌ | ✅ |

## GSA Proxy Details

The GSA AI Connector is deployed from the Azure Marketplace:
- **Publisher**: `microsoftcorporation1687208452115`
- **Offer**: `gsaaiconnector1-preview`
- **Plan**: `gsaaiconnectorplan1`

### VM Configuration
- **Managed Identity**: System-assigned (enabled)
- **IP Forwarding**: Enabled on the NIC (required for routing)
- **NSG Rules**:
  - Inbound: Allow `VirtualNetwork` and `AzureLoadBalancer`; deny all other
  - Outbound: Allow `VirtualNetwork` and `Internet`

## UDR (User Defined Routes)

The agent subnet has a route table that sends all default (`0.0.0.0/0`) traffic through the GSA proxy VM as a virtual appliance. The following Azure service tags are **excepted** (routed directly to Internet):

| Service Tag | Purpose |
|---|---|
| `AzureActiveDirectory` | Entra ID authentication |
| `AzureResourceManager` | ARM API calls |
| `AzureMonitor` | Monitoring and diagnostics |
| `GuestAndHybridManagement` | VM guest agent management |
| `AzureContainerRegistry` | Container image pulls |
| `AzureKeyVault` | Key Vault access |
| `Storage` | Azure Storage access |

## Prerequisites

1. **Accept the marketplace terms** for the GSA AI Connector image before deploying:
   ```bash
   az vm image terms accept \
     --publisher microsoftcorporation1687208452115 \
     --offer gsaaiconnector1-preview \
     --plan gsaaiconnectorplan1
   ```

2. **Generate an SSH key pair** for the proxy VM:
   ```bash
   ssh-keygen -t rsa -b 4096 -f ~/.ssh/gsa-proxy-key -N ""
   ```

## Deployment

### Using Azure CLI with Bicep parameter file

```bash
# Set your SSH public key
export GSA_PROXY_SSH_PUBLIC_KEY=$(cat ~/.ssh/gsa-proxy-key.pub)

# Create resource group
az group create --name rg-ai-agent-gsa-proxy --location eastus

# Deploy
az deployment group create \
  --resource-group rg-ai-agent-gsa-proxy \
  --template-file main.bicep \
  --parameters main.bicepparam
```

### Using inline parameters

```bash
az deployment group create \
  --resource-group rg-ai-agent-gsa-proxy \
  --template-file main.bicep \
  --parameters \
    location=eastus \
    aiServices=foundry \
    gsaProxySshPublicKey="$(cat ~/.ssh/gsa-proxy-key.pub)"
```

## Module Structure

```
46-standard-agent-byovnet-gsa-proxy-setup/
├── main.bicep                              # Orchestrator - deploys everything
├── main.bicepparam                         # Parameter file
├── README.md                               # This file
└── modules/
    ├── vnet.bicep                           # VNet with agent + proxy subnets
    ├── gsa-proxy.bicep                      # GSA proxy VM, NIC, NSG, UDR
    ├── agent-subnet-udr-association.bicep   # Associates UDR to agent subnet
    ├── ai-account-identity.bicep            # AI Services with subnet injection
    ├── ai-project-identity.bicep            # AI Project
    ├── standard-dependent-resources.bicep   # Cosmos DB, AI Search, Storage
    ├── add-project-capability-host.bicep    # Capability host configuration
    ├── validate-existing-resources.bicep    # Validates BYO resources
    ├── format-project-workspace-id.bicep    # Workspace ID formatting
    ├── ai-search-role-assignments.bicep     # AI Search RBAC
    ├── azure-storage-account-role-assignment.bicep  # Storage RBAC
    ├── blob-storage-container-role-assignments.bicep # Blob container RBAC
    ├── cosmos-container-role-assignments.bicep       # Cosmos container RBAC
    └── cosmosdb-account-role-assignment.bicep        # Cosmos account RBAC
```

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `location` | string | `eastus` | Azure region |
| `aiServices` | string | `foundry` | AI Services resource name (max 9 chars) |
| `firstProjectName` | string | `project` | Project resource name |
| `vnetName` | string | `agent-vnet` | Virtual network name |
| `vnetAddressPrefix` | string | `10.0.0.0/16` | VNet CIDR |
| `agentSubnetPrefix` | string | `10.0.0.0/24` | Agent subnet CIDR |
| `gsaProxySubnetPrefix` | string | `10.0.1.0/24` | GSA proxy subnet CIDR |
| `gsaProxyVmSize` | string | `Standard_D2s_v3` | Proxy VM size |
| `gsaProxySshPublicKey` | secure string | *(required)* | SSH public key for VM |
| `modelName` | string | `gpt-4.1` | Model to deploy |
| `modelCapacity` | int | `30` | TPM for model deployment |
| `aiSearchResourceId` | string | `''` | Optional existing AI Search ARM ID |
| `azureStorageAccountResourceId` | string | `''` | Optional existing Storage ARM ID |
| `azureCosmosDBAccountResourceId` | string | `''` | Optional existing Cosmos DB ARM ID |