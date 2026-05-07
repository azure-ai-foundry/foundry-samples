---
description: This set of templates demonstrates how to set up Foundry Agent Service with virtual network isolation with private network links to connect the agent to your secure data.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: network-secured-agent
languages:
- bicep
- json
---

# Microsoft Foundry: Standard Agent Setup with E2E Network Isolation (without Tools behind VNET)

> **NEW**
> For support on deploying the right network isolation template, check out the [GitHub Copilot for Azure skill for private networking](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/microsoft-foundry/resource/private-network/private-network.md) set-up!

> **IMPORTANT**
> Please note this template does not support using Agent tools behind a VNET. Please refer to [template 19](../19-private-network-agents-tools-setup/) and the [TESTING-GUIDE.md](../19-private-network-agents-tools-setup/tests/TESTING-GUIDE.md) to ensure tool traffic also goes through your vnet. 

---
## Overview
This infrastructure-as-code (IaC) solution deploys a network-secured agent environment with private networking and role-based access control (RBAC).

Standard setup supports private network isolation through utilizing **Bring Your Own Virtual Network (BYO VNet)** approach, also known as **custom VNet support with subnet delegation.** Please note this template does not support using Agent tools behind a VNET. Please use [template 19](../19-private-network-agents-tools-setup/) for this.  

This implementation gives you full control over the inbound and outbound communication paths for your agent. You can restrict access to only the resources explicitly required by your agent, such as storage accounts, databases, or APIs, while blocking all other traffic by default. This approach ensures that your agent operates within a tightly scoped network boundary, reducing the risk of data leakage or unauthorized access. By default, this setup simplifies security configuration while enforcing strong isolation guarantees, ensuring that each agent deployment remains secure, compliant, and aligned with enterprise networking policies. 

---

## When to Use This Template

Use this template when you need:
- **Full end-to-end network isolation** — All resources behind private endpoints with no public internet access
- **BYO VNet control** — You manage your own virtual network, subnets, and network security groups
- **Standard agent setup with BYO resources** — Customer-managed Storage, Cosmos DB, and AI Search for data residency and compliance
- **System Assigned Managed Identity** — Simplified identity management with platform-managed credentials

### Template Decision Guide

Use the table below to choose the right infrastructure template for your scenario:

| Template | Agent Type | Networking | Identity | Key Use Case |
|----------|-----------|------------|----------|-------------|
| [**15** (this template)](../15-private-network-standard-agent-setup/) | Standard (BYO resources) | BYO VNet + Private Endpoints | System Assigned MI | E2E network isolation with full agent capabilities |
| [**19**](../19-private-network-agents-tools-setup/) | Standard (BYO resources) | BYO VNet + Private Endpoints | System Assigned MI | Same as 15 **plus** tools behind VNet (MCP, OpenAPI, Functions, A2A) |
| [**17**](../17-private-network-standard-user-assigned-identity-agent-setup/) | Standard (BYO resources) | BYO VNet + Private Endpoints | **User Assigned MI** | Same as 15 but with user-managed identity |
| [**16**](../16-private-network-standard-agent-apim-setup-preview/) | Standard (BYO resources) | BYO VNet + Private Endpoints | System Assigned MI | Same as 15 **plus** private APIM integration (preview) |
| [**18**](../18-managed-virtual-network-preview/) | Standard (BYO resources) | **Managed VNet** (Microsoft-managed) | System Assigned MI | Network isolation without managing your own VNet (preview) |
| [**15a**](../15a-private-network-evaluation-only-setup/) | Evaluation only | BYO VNet + Private Endpoints | System Assigned MI | Minimal setup for evaluation — no Cosmos DB, AI Search, or capability host |
| [**11**](../11-private-network-basic-vnet/) | **Basic** (platform-managed) | BYO VNet injection | System Assigned MI | Basic agents with VNet isolation — no BYO resources needed |
| [**41**](../41-standard-agent-setup/) | Standard (BYO resources) | **Public** (no VNet) | System Assigned MI | Standard agents without network isolation |
| [**40**](../40-basic-agent-setup/) | **Basic** (platform-managed) | **Public** (no VNet) | System Assigned MI | Simplest setup — no BYO resources, no private networking |

---

## Deploy to Azure

[![Deploy To Azure](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/deploytoazure.svg?sanitize=true)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fazure-ai-foundry%2Ffoundry-samples%2Frefs%2Fheads%2Fmain%2Finfrastructure%2Finfrastructure-setup-bicep%2F15-private-network-standard-agent-setup%2Fazuredeploy.json)

---

## Prerequisites

1. **Active Azure subscription with appropriate permissions**
  - **Azure AI Account Owner**: Needed to create the Microsoft Foundry account and project.
  - **Owner or Role Based Access Administrator**: Needed to assign RBAC on the Azure resources used by this template.
  - **Azure AI User**: Needed to create and use agents, projects, or evaluation workloads after deployment.

1. **Register Resource Providers**

   Make sure you have an active Azure subscription that allows registering resource providers. For example, subnet delegation requires the Microsoft.App provider to be registered in your subscription. If it's not already registered, run the commands below:

   ```bash
   az provider register --namespace 'Microsoft.KeyVault'
   az provider register --namespace 'Microsoft.CognitiveServices'
   az provider register --namespace 'Microsoft.Storage'
   az provider register --namespace 'Microsoft.Search'
   az provider register --namespace 'Microsoft.Network'
   az provider register --namespace 'Microsoft.App'
   az provider register --namespace 'Microsoft.ContainerService'
   ```

1. Network administrator permissions (if operating in a restricted or enterprise environment)

1. Sufficient quota for all resources required by this template in the target Azure region, including model deployment quota.
    * If no parameters are passed in, this template creates an Microsoft Foundry resource, Foundry project, Azure Cosmos DB for NoSQL, Azure AI Search, and Azure Storage account
1. Azure CLI installed and configured on your local workstation or deployment pipeline server

---

## Pre-Deployment Steps

### Networking Requirements
1. Review network requirements and plan Virtual Network address space (e.g., 192.168.0.0/16 or an alternative non-overlapping address space)

2. Two subnets are needed as well:  
    - **Agent Subnet** (e.g., 192.168.0.0/24): Hosts Agent client for Agent workloads, delegated to Microsoft.App/environments. The recommended size should be /24 for this delegated subnet. 
    - **Private endpoint Subnet** (e.g. 192.168.1.0/24): Hosts private endpoints 
    - Ensure that the address spaces for the used VNET does not overlap with any existing networks in your Azure environment or reserved IP ranges like the following: 169.254.0.0/16,172.30.0.0/16,172.31.0.0/16,192.0.2.0/24,0.0.0.0/8,127.0.0.0/8,100.100.0.0/17,100.100.192.0/19,100.100.224.0/19,100.64.0.0/11.
    This includes all address space(s) you have in your VNET if you have more than one, and peered VNETs.
  
  > **Notes:** 
  - If you do not provide an existing virtual network, the template will create a new virtual network with the default address spaces and subnets described above. If you use an existing virtual network, make sure it already contains two subnets (Agent and Private Endpoint) before deploying the template.
  - The account-level capability host is now provisioned declaratively by `modules-network-secured/add-account-capability-host.bicep` as part of `main.bicep`. The standalone `createCapHost.sh` script is no longer required for first-time deployments; it remains in the folder only to support the cleanup-then-recreate flow described in the [Account Deletion Prerequisites and Cleanup Guidance](#account-deletion-prerequisites-and-cleanup-guidance).
  - You must ensure the subnet is exclusively delegated to __Microsoft.App/environments__ and cannot be used by any other Azure resources.



### Limitations / Known Issues

1. The delegated agent subnet must be exclusively used by a single Foundry account. It cannot be shared across accounts.
2. The Foundry resource and the virtual network must be in the same Azure region. BYO resources (Storage, Cosmos DB, AI Search) may be in different regions.
3. For the virtual network IP range, you may use any Private Class A, B or C IP range. Private Class A IP address ranges (10.x.x.x) are only supported in the following regions: **Australia East, Brazil South, Canada East, East US, East US 2, France Central, Germany West Central, Italy North, Japan East, South Africa North, South Central US, South India, Spain Central, Sweden Central, UAE North, UK South, West US, West US 3.** Use Class B (172.16.x.x) or C (192.168.x.x) ranges for other regions. You may not use any other IP range that overlaps to the list above or uses public IP ranges. 
4. This template does **not** support tools (MCP servers, OpenAPI tools, Azure Functions, A2A) behind the VNet. Use [template 19](../19-private-network-agents-tools-setup/) for that scenario.
5. There is no upgrade path from BYO VNet (this template) to Managed Virtual Network (template 18). A Foundry resource redeployment is required.
6. All projects within the same Foundry account share model deployments. Per-project model isolation is not supported.
7. Cosmos DB is deployed as single-region. Multi-region replication must be configured manually post-deployment.
8. When reusing an existing Foundry account (`existingAiFoundryAccountResourceId`), the template will not create a new model deployment if `skipModelDeployment` is set to `true`. The required model deployment(s) must already exist on the BYO account.

### Account Deletion Prerequisites and Cleanup Guidance

Before deleting an **Account** resource, it is essential to first delete the associated **Account Capability Host**. Failure to do so may result in residual dependencies—such as subnets and other provisioned resources (e.g., ACA applications)—remaining linked to the capability host. This can lead to errors such as **"Subnet already in use"** when attempting to reuse the same subnet in a different account deployment.

**Cleanup Options**

**1. Full Account Removal**: To completely remove an account, you must delete and purge the account. Simply deleting the account is not sufficient, you must purge so that deletion of the associated capability host is triggered. The service will automatically handle the removal of the capability host and any linked resources in the background. To purge the account, use the following [link](https://learn.microsoft.com/en-us/azure/ai-services/recover-purge-resources?tabs=azure-portal#purge-a-deleted-resource). Please allow approximately max of 20 minutes for all resources to be fully unlinked from the account.
 
**2. Retain Account, Remove Capability Host**: If you intend to retain the account but remove the capability host, execute the script `deleteCaphost.sh` located in this folder. After deletion, allow approximately max of 20 minutes for all resources to be fully unlinked from the account. To recreate the capability host for the account, use the script `createCaphost.sh` located in the same folder.

> **Note**: The account-level capability host is created declaratively by `main.bicep` (via `modules-network-secured/add-account-capability-host.bicep`) on first deployment. The `createCapHost.sh` script is intended for this cleanup-then-recreate scenario only; it is not required for an initial deployment.


> **Important**: Before deleting the account capability host, ensure that the **project capability host** is deleted.

### Template Customization

Note: If not provided, the following resources will be created automatically for you:
- VNet and two subnets
- Azure Cosmos DB for NoSQL  
- Azure AI Search
- Azure Storage

#### Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `location` | Azure region for deployment | `eastus` | Yes |
| `aiServices` | Base name for the AI Services resource | `aiservices` | No |
| `firstProjectName` | Name for the Foundry project | `project` | No |
| `modelName` | Model to deploy | `gpt-4.1` | No |
| `modelFormat` | Model provider | `OpenAI` | No |
| `modelVersion` | Model version | `2025-04-14` | No |
| `modelSkuName` | Model deployment SKU | `GlobalStandard` | No |
| `modelCapacity` | Tokens per minute (TPM) capacity | `30` | No |
| `skipModelDeployment` | When `true`, skip creating a model deployment. Recommended when reusing an existing Foundry account that already has the required model deployments. | `false` | No |
| `vnetName` | Virtual Network name. When `existingVnetResourceId` is set, the name is derived from that resource ID and this parameter is ignored. When creating a new VNet, leave empty to use the generated default. | `''` | No |
| `agentSubnetName` | Subnet name for agent workloads | `agent-subnet` | No |
| `agentSubnetPrefix` | Address prefix for agent subnet | `192.168.0.0/24` | No |
| `peSubnetName` | Subnet name for private endpoints | `pe-subnet` | No |
| `peSubnetPrefix` | Address prefix for PE subnet | `192.168.1.0/24` | No |
| `existingVnetResourceId` | Full ARM Resource ID of an existing VNet | `''` (creates new) | No |
| `reuseExistingSubnets` | When `true` and `existingVnetResourceId` is set, the template will reference your existing subnets without modifying them. Use this when your subnets are already configured by your platform team (NSG, route tables, private endpoint network policies) and tenant policies forbid changes. | `false` | No |
| `vnetAddressPrefix` | Address space for new VNet | `192.168.0.0/16` | No |
| `aiSearchResourceId` | ARM Resource ID of existing AI Search | `''` (creates new) | No |
| `azureStorageAccountResourceId` | ARM Resource ID of existing Storage account | `''` (creates new) | No |
| `azureCosmosDBAccountResourceId` | ARM Resource ID of existing Cosmos DB | `''` (creates new) | No |
| `existingAiFoundryAccountResourceId` | Full ARM Resource ID of an existing Microsoft Foundry (Cognitive Services / AIServices) account to reuse. When set, the template will not create a new account. | `''` (creates new) | No |
| `dnsZonesSubscriptionId` | Subscription ID for existing DNS zones. Accepts either a bare GUID (`<subscription-id>`) or a full ARM subscription path (`/subscriptions/<subscription-id>`); the template normalizes the value internally. | `''` (current sub) | No |
| `existingDnsZones` | Map of DNS zone names to resource groups | All empty (creates new) | No |

#### BYO Resource Details

1. **Use Existing Virtual Network and Subnets**

To use an existing VNet and subnets, set the existingVnetResourceId parameter to the full Azure Resource ID of the target VNet and its address range, and provide the names of the two required subnets.  If the existing VNet is associated with private DNS zones, set the existingDnsZones parameter to the resource group name in which the zones are located. For example:
- param existingVnetResourceId = "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/virtualNetworks/<vnet-name>"
- param agentSubnetName string = 'agent-subnet' //optional, default is 'agent-subnet'
- param agentSubnetPrefix string = '192.168.0.0/24' //optional, default is '192.168.0.0/24'
- param peSubnetName string = 'pe-subnet' //optional, default is 'pe-subnet'
- param peSubnetPrefix string = '192.168.1.0/24' //optional, default is '192.168.1.0/24'
- param dnsZonesSubscriptionId string = '' //optional, leave empty to use current subscription, or set to a subscription ID if DNS zones are in a different subscription
- param existingDnsZones = {
       
         'privatelink.services.ai.azure.com': 'privzoneRG' //add resource group name where your private DNS zone is located
       
         'privatelink.openai.azure.com': '' //Leave empty to create new private dns zone... }

💡 If subnets information is provided then make sure it exist within the specified VNet to avoid deployment errors. If subnet information is not provided, the template will create subnets with the default address space.

💡 **Reuse pre-configured subnets**: If your subnets are already configured by your platform team (NSG, route tables, `privateEndpointNetworkPolicies` set per tenant policy), set `reuseExistingSubnets = true`. This tells the template to reference the subnets without re-applying their configuration, which prevents an inadvertent reset of subnet properties on redeploy.

💡 **Cross-Subscription DNS Zones**: All DNS zones specified in `existingDnsZones` will be referenced from the subscription specified in `dnsZonesSubscriptionId`. Leave this parameter empty (default) to use the current deployment subscription, or set it to a subscription ID if your DNS zones are located in a different subscription. The parameter accepts either a bare subscription GUID or a full ARM subscription path (`/subscriptions/<subscription-id>`); the template normalizes the value internally.

⚠️ **Important**: When `dnsZonesSubscriptionId` is set to a different subscription, ALL DNS zones in `existingDnsZones` must have resource groups specified (non-empty values). The template does not support creating new DNS zones in a different subscription. Empty resource groups are only allowed when creating zones in the current deployment subscription.


2. **Use an existing Azure Cosmos DB for NoSQL**

To use an existing Cosmos DB for NoSQL resource, set cosmosDBResourceId parameter to the full Azure Resource ID of the target Cosmos DB.
- param azureCosmosDBAccountResourceId string =  /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmosDbAccountName}


3. **Use an existing Azure AI Search resource**

To use an existing Azure AI Search resource, set aiSearchServiceResourceId parameter to the full Azure resource Id of the target Azure AI Search resource. 
 - param aiSearchResourceId string = /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Search/searchServices/{searchServiceName}


4. **Use an existing Azure Storage account**

To use an existing Azure Storage account, set aiStorageAccountResourceId parameter to the full Azure resource Id of the target Azure Storage account resource. 
- param aiStorageAccountResourceId string = /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}


5. **Use an existing Microsoft Foundry account**

To reuse an existing Microsoft Foundry (Cognitive Services / AIServices kind) account instead of creating a new one, set `existingAiFoundryAccountResourceId` to the full Azure Resource ID of the target account. The template will reference the existing account, scope the account-level capability host to its resource group and subscription, and skip the deterministic-suffix account creation path (which would otherwise create a new account on every redeploy).

- param existingAiFoundryAccountResourceId string = '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.CognitiveServices/accounts/{accountName}'
- param skipModelDeployment bool = true  // recommended when the BYO account already has the required model deployment(s)

💡 **When to use this**: bring-your-own-account is intended for scenarios where the Foundry account is provisioned ahead of time by a platform team or landing zone, and the workload deployment must reuse it (for compliance, naming standards, or to avoid orphaned accounts on retry).

⚠️ **Important**: When `existingAiFoundryAccountResourceId` is set, the required model deployment(s) must already exist on the BYO account if `skipModelDeployment = true`. The agent service depends on at least one model deployment matching `modelName` / `modelVersion` to function.

---

## Deploy the bicep template

Choose your deployment method: Use the "Deploy to Azure" button from the provided README for an guided experience in Azure Portal

**Option 1: Automatic deployment** 
Click the deploy to Azure button above to open the Azure portal and deploy the template directly. 
- Fill in the parameters as needed, including the existing VNet and subnets if applicable. 


**Option 2: Manually deploy the bicep template**
- **Create a New (or Use Existing) Resource Group**

   ```bash
   az group create --name <new-rg-name> --location <your-rg-region>
   ```
- Deploy the main.bicep file
  - Edit the main.bicepparams file to use an existing Virtual Network & subnets, Azure Cosmos DB, Azure Storage, and Azure AI Search.

   ```bash
      az deployment group create --resource-group <your-resource-group> --template-file main.bicep --parameters main.bicepparam
   ```

> **Note:** To access a private Foundry resource securely, use one of the following:
> - A VM or jump box on the virtual network, optionally accessed through Azure Bastion
> - Azure VPN Gateway
> - Azure ExpressRoute

### Cleanup

To delete all resources created by this template:

```bash
az group delete --name <your-resource-group> --yes --no-wait
```

> **Important**: If you need to reuse the same subnet, follow the [Account Deletion Prerequisites and Cleanup Guidance](#account-deletion-prerequisites-and-cleanup-guidance) to properly purge the account and wait for the capability host to fully unlink (~20 minutes).

---  

## Network Secured Agent Project Architecture Deep Dive

```
┌─────────────────────────────────────────────────────────────────────┐
│  Secure Access (VPN Gateway / ExpressRoute / Azure Bastion)         │
└──────────────────────────────────┬──────────────────────────────────┘
                                   │
                    ┌──────────────▼──────────────┐
                    │   Microsoft Foundry          │
                    │   (publicNetworkAccess:      │
                    │        DISABLED)             │
                    │                              │
                    │  ┌────────────────────────┐  │
                    │  │   Foundry Project       │  │
                    │  │   (Agent Workspace)     │  │
                    │  └───────────┬────────────┘  │
                    └──────────────┼──────────────┘
                                   │ Subnet Delegation
                    ┌──────────────▼──────────────┐
                    │   BYO Virtual Network        │
                    │   (192.168.0.0/16)           │
                    │                              │
                    │  ┌──────────────────────┐    │
                    │  │ Agent Subnet          │   │
                    │  │ (192.168.0.0/24)      │   │  ◄── Delegated to
                    │  │ Microsoft.App/envs    │   │      Microsoft.App/environments
                    │  └──────────────────────┘    │
                    │                              │
                    │  ┌──────────────────────┐    │
                    │  │ PE Subnet             │   │
                    │  │ (192.168.1.0/24)      │   │
                    │  │                       │   │
                    │  │ ┌────────┐ ┌────────┐ │   │
                    │  │ │Storage │ │Cosmos  │ │   │  ◄── Private endpoints
                    │  │ └────────┘ └────────┘ │   │      (no public access)
                    │  │ ┌────────┐ ┌────────┐ │   │
                    │  │ │Search  │ │Foundry │ │   │
                    │  │ └────────┘ └────────┘ │   │
                    │  └──────────────────────┘    │
                    └──────────────────────────────┘
```

> **Tip:** For detailed layer-by-layer deployment diagrams, see the `diagrams/` folder.

### Core Components

**Microsoft Foundry** resource
- Central orchestration point
- Manages service connections
- Set networking and policy configurations

**Foundry** project
- Defines the workspace configuration 
- Service integration 
- Agents are created within a specific project, and each project acts as an isolated workspace. This means:
  - All agents in the same project share access to the same file storage, thread storage (conversation history), and search indexes.
  - Data is isolated between projects. Agents in one project cannot access resources from another. Projects are currently the unit of  sharing and isolation in Foundry. See the what is AI foundry article for more information on Foundry projects. 

**Bring Your Own (BYO) Azure Resources**: ensures all sensitive data remains under customer control. All agents created using our service are stateful, meaning they retain information across interactions. With this setup, agent states are automatically stored in customer-managed, single-tenant resources. The required Bring Your Own Resources include: 
- BYO File Storage: All files uploaded by developers (during agent configuration) or end-users (during interactions) are stored directly in the customer’s Azure Storage account.
- BYO Search: All vector stores created by the agent leverage the customer’s Azure AI Search resource.
- BYO Thread Storage: All customer messages and conversation history will be stored in the customer’s own Azure Cosmos DB account.

By bundling these BYO features (file storage, search, and thread storage), the standard setup guarantees that your deployment is secure by default. All data processed by Microsoft Foundry Agent Service is automatically stored at rest in your own Azure resources, helping you meet internal policies, compliance requirements, and enterprise security standards.

### Azure Resources Created

Microsoft Foundry (Cognitive Services)
- Type: Microsoft.CognitiveServices/accounts
- API version: 2025-04-01-preview
- Kind: AIServices
- SKU: S0
- Identity: System-assigned
- Features:
  - Custom subdomain name
  - Disabled public network access
  - Network ACLs with Azure Services bypass 

AI Model Deployment 
- Type: Microsoft.CognitiveServices/accounts/deployments 
- API version: 2025-04-01-preview
- SKU: Based on modelSkuName parameter, capacity set by modelCapacity 
- Model properties:
  - Name: From modelName parameter
  - Format: From modelFormat parameter
  - Version: From modelVersion parameter 

Azure AI Search 
- Type: Microsoft.Search/searchServices
- API version: 2024-06-01-preview
- SKU: standard 
- Partition Count: 1 
- Replica Count: 1 
- Hosting Mode: default 
- Semantic Search: disabled
- Features:
  -  Disabled public network access
  -  AAD auth with HTTP 401 challenge
  -  System-assigned managed identity

Storage Account 
- Type: Microsoft.Storage/storageAccounts 
- API version: 2023-05-0
- Kind: StorageV2 
- SKU: ZRS or GRS (region dependent; use Standard_GRS if ZRS not available) 
- Features:
  - Blob service, Queue service (if Azure Function Tool supported)
  - Minimum TLS Version: 1.2
  - Block public blob access
  - Disabled public network access
  - Force Azure AD authentication (SharedKey access disabled) 

Cosmos DB Account 
- Type: Microsoft.DocumentDB/databaseAccounts 
- API version: 2024-11-15 
- Kind: GlobalDocumentDB (SQL API) 
- Consistency Level: Session 
- Database Account Offer Type: Standard 
- Features:
  - Disabled public network access
  - Disabled local auth
  - Single region deployment 

### Network Security Design
This implementation utilizes a BYO VNet (Bring Your Own Virtual Network) approach, also known as custom VNet support with subnet delegation. Within your existing virtual network, one delegated subnet will be created.

Network Security
- Public network access disabled
- Private endpoints for all services
- Network ACLs with deny by default

**Network Infrastructure**
- A Virtual Network (192.168.0.0/16) is created (if existing isn't passed in)
- Agent Subnet (192.168.0.0/24): Hosts Agent client
- Private endpoint Subnet (192.168.1.0/24): Hosts private endpoints

**Private Endpoints** 
Private endpoints ensure secure, internal-only connectivity. Private endpoints are created for the following:
- Microsoft Foundry
- Azure AI Search
- Azure Storage
- Azure Cosmos DB

**Private DNS Zones**
| Private Link Resource Type | Sub Resource | Private DNS Zone Name | Public DNS Zone Forwarders |
|----------------------------|--------------|------------------------|-----------------------------|
| **Microsoft Foundry**       | account      | `privatelink.cognitiveservices.azure.com`<br>`privatelink.openai.azure.com`<br>`privatelink.services.ai.azure.com` | `cognitiveservices.azure.com`<br>`openai.azure.com`<br>`services.ai.azure.com` |
| **Azure AI Search**        | searchService| `privatelink.search.windows.net` | `search.windows.net` |
| **Azure Cosmos DB**        | Sql          | `privatelink.documents.azure.com` | `documents.azure.com` |
| **Azure Storage**          | blob         | `privatelink.blob.core.windows.net` | `blob.core.windows.net` |

### Authentication & Authorization

- **Managed Identity**
  - Zero-trust security model
  - No credential storage
  - Platform-managed rotation

  This template uses System Managed Identity, but User Assigned Managed Identity is also supported.

- **Role Assignments**
  - **Azure AI Search**
    - Search Index Data Contributor (`8ebe5a00-799e-43f5-93ac-243d3dce84a7`)
    - Search Service Contributor (`7ca78c08-252a-4471-8644-bb5ff32d4ba0`)
  - **Azure Storage Account**
    - Storage Blob Data Owner (`b7e6dc6d-f1e8-4753-8033-0f276bb0955b`)
    - Storage Queue Data Contributor (`974c5e8b-45b9-4653-ba55-5f855dd0fb88`) (if Azure Function tool enabled)
    - Two containers will automatically be provisioned during the project create capability host process:
      - Azure Blob Storage Container: `<workspaceId>-azureml-blobstore`
        - Storage Blob Data Contributor
      - Azure Blob Storage Container: `<workspaceId>-agents-blobstore`
        - Storage Blob Data Owner
  - **Cosmos DB for NoSQL**
    - Cosmos DB Operator (`230815da-be43-4aae-9cb4-875f7bd000aa`)
    - Cosmos DB Built-in Data Contributor
    - Three containers will automatically be provisioned during the create capability host process:
      - Cosmos DB for NoSQL container: `<${projectWorkspaceId}>-thread-message-store`
      - Cosmos DB for NoSQL container: `<${projectWorkspaceId}>-system-thread-message-store`
      - Cosmos DB for NoSQL container: `<${projectWorkspaceId}>-agent-entity-store`


---

## Module Structure

```text
modules-network-secured/
├── add-account-capability-host.bicep               # Declarative account-level capability host (replaces createCapHost.sh for first-time deployments)
├── add-project-capability-host.bicep               # Configuring the project's capability host
├── ai-account-identity.bicep                       # Microsoft Foundry deployment and configuration (supports BYO existing account)
├── ai-project-identity.bicep                       # Foundry project deployment and connection configuration           
├── ai-search-role-assignments.bicep                # AI Search RBAC configuration
├── azure-storage-account-role-assignments.bicep    # Storage Account RBAC configuration  
├── blob-storage-container-role-assignments.bicep   # Blob Storage Container RBAC configuration
├── cosmos-container-role-assignments.bicep         # CosmosDB container Account RBAC configuration
├── cosmosdb-account-role-assignment.bicep          # CosmosDB Account RBAC configuration
├── existing-vnet.bicep                             # Bring your existing virtual network to template deployment
├── format-project-workspace-id.bicep               # Formatting the project workspace ID
├── network-agent-vnet.bicep                        # Logic for routing virtual network set-up if existing virtual network is selected
├── private-endpoint-and-dns.bicep                  # Creating virtual networks and DNS zones. 
├── standard-dependent-resources.bicep              # Deploying CosmosDB, Storage, and Search
├── subnet.bicep                                    # Setting the subnet for Agent network injection
├── validate-existing-resources.bicep               # Validate existing CosmosDB, Storage, and Search to template deployment
└── vnet.bicep                                      # Deploying a new virtual network
```

## Maintenance

### Regular Tasks

1. Review role assignments
2. Monitor network security
3. Check service health
4. Update configurations as needed

### Troubleshooting

1. Verify private endpoint connectivity
2. Check DNS resolution
3. Validate role assignments
4. Review network security groups

---
# (Optional) Adding Multiple Projects to AI Foundry Deployment

This guide explains how to add additional projects to your existing AI Foundry deployment with network security and capability hosts.

## Overview

After deploying your initial AI Foundry setup using `main.bicep`, you can add additional projects using the modular approach provided in this repository. Each new project will:

- ✅ **Reuse existing shared infrastructure** (AI Services account, Storage, Cosmos DB, AI Search, VNet)
- ✅ **Create independent projects** with unique identities and connections
- ✅ **Set up proper role assignments** and capability hosts for each project
- ✅ **Maintain network security** configurations from your original deployment
- ✅ **Deploy independently** without affecting existing projects

## Files Added

### Core Deployment Files

| File | Purpose |
|------|---------|
| `add-project.bicep` | Main Bicep template for adding new projects |
| `add-project.bicepparam` | Parameters file template for new projects |
| `modules-network-secured/ai-project-identity-unique.bicep` | Modified project module with unique connection names |
| `modules-network-secured/blob-storage-container-role-assignments-unique.bicep` | Modified storage role assignment module |

### Helper Files

| File | Purpose |
|------|---------|
| `get-existing-resources.ps1` | PowerShell script to discover existing resource names |

## Prerequisites

1. ✅ **Existing AI Foundry deployment** completed using `main.bicep`
2. ✅ **Azure CLI** installed and logged in
3. ✅ **Proper permissions** on the resource group and existing resources
4. ✅ **Resource names** from your existing deployment

## Step-by-Step Guide

### Step 1: Discover Existing Resource Names

Run the PowerShell script to automatically discover your existing resource names:

```powershell
# Navigate to your repository folder
cd "path\to\your\AgentRepro\folder"

# Run the discovery script
.\get-existing-resources.ps1 -ResourceGroupName "your-resource-group-name"

# Optional: Include subscription ID if needed
.\get-existing-resources.ps1 -ResourceGroupName "your-resource-group-name" -SubscriptionId "your-subscription-id"
```

**Example output:**
```
=== Summary for add-project.bicepparam ===
param existingAccountName = 'aiservicesytlz'
param existingAiSearchName = 'aiservicesytlzsearch'
param existingStorageName = 'aiservicesytlzstorage'
param existingCosmosDBName = 'aiservicesytlzcosmosdb'
param accountResourceGroupName = 'agenticvnet'
param aiSearchResourceGroupName = 'agenticvnet'
param storageResourceGroupName = 'agenticvnet'
param cosmosDBResourceGroupName = 'agenticvnet'
```

### Step 2: Configure Parameters File

Copy the output from Step 1 and update your `add-project.bicepparam` file:

### Step 3: Deploy the New Project

Deploy using Azure CLI:

```powershell
az deployment group create `
  --resource-group "your-resource-group" `
  --template-file "add-project.bicep" `
  --parameters "add-project.bicepparam"
```

## Adding Multiple Projects

To add additional projects, repeat the process with different parameter values:

### For a Third Project:

1. **Update project-specific parameters:**
   ```bicep
   param projectName = 'thirdproject'  // Must be unique
   param displayName = 'Third Project'
   param projectCapHost = 'caphostthird'  // Must be unique
   ```

3. **Deploy using the new parameters file:**
   ```powershell
   az deployment group create `
     --resource-group "your-resource-group" `
     --template-file "add-project.bicep" `
     --parameters "add-project.bicepparam"
   ```

## What Gets Created

Each new project deployment creates:

| Resource | Description |
|----------|-------------|
| **AI Foundry Project** | New project under your existing AI Services account |
| **Managed Identity** | Project-specific system-assigned identity |
| **Unique Connections** | Project-specific connections to shared resources |
| **Capability Host** | Configured for Agents with proper connections |
| **RBAC Assignments** | Proper permissions on shared resources |

### Role Assignments Created:

- ✅ **Storage Blob Data Contributor** on Storage Account
- ✅ **Storage Blob Data Owner** on project-specific containers
- ✅ **Cosmos DB Operator** on Cosmos DB Account
- ✅ **Cosmos Built-In Data Contributor** on project-specific containers
- ✅ **Search Index Data Contributor** on AI Search Service
- ✅ **Search Service Contributor** on AI Search Service

## Configuration Reference

### Required Parameters (Must Customize for Each Project)

| Parameter | Description | Example |
|-----------|-------------|---------|
| `projectName` | Unique name for the project | `'secondproject'` |
| `displayName` | Display name in Azure portal | `'Second Project'` |
| `projectCapHost` | Unique capability host name | `'caphostsecond'` |
| `projectDescription` | Description of the project | `'My second AI project'` |

### Existing Resource Parameters (From Script)

| Parameter | Description | Source |
|-----------|-------------|---------|
| `existingAccountName` | AI Services account name | Output from `get-existing-resources.ps1` |
| `existingAiSearchName` | AI Search service name | Output from `get-existing-resources.ps1` |
| `existingStorageName` | Storage account name | Output from `get-existing-resources.ps1` |
| `existingCosmosDBName` | Cosmos DB account name | Output from `get-existing-resources.ps1` |
| `*ResourceGroupName` | Resource group names | Usually same as deployment RG |
| `*SubscriptionId` | Subscription IDs | Usually same subscription |


## Security Considerations

- ✅ **Least Privilege**: Each project gets only the permissions it needs
- ✅ **Isolated Containers**: Projects get separate storage containers
- ✅ **Network Security**: Inherits network security from original deployment
- ✅ **Unique Identities**: Each project has its own managed identity

## Limitations

- 📝 All projects share the same model deployments
- 📝 Projects must be in the same region as the original deployment
- 📝 Network configuration is inherited from original deployment

## References

- [Microsoft Foundry Networking Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link?tabs=azure-portal&pivots=fdp-project)
- [Microsoft Foundry RBAC Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/rbac-azure-ai-foundry?pivots=fdp-project)
- [Private Endpoint Documentation](https://learn.microsoft.com/en-us/azure/private-link/)
- [RBAC Documentation](https://learn.microsoft.com/en-us/azure/role-based-access-control/)
- [Network Security Best Practices](https://learn.microsoft.com/en-us/azure/security/fundamentals/network-best-practices)
