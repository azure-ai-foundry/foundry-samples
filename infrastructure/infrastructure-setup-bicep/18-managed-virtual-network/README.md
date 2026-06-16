---
description: This set of templates demonstrates how to set up Microsoft Foundry Agent Service with managed virtual network isolation with private network links to connect the agent to your secure data.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: managed-network-secured-agent
languages:
- bicep
- json
---

# Microsoft Foundry: Standard Agent Setup with Managed Virtual Network

> **NEW**
> For support on deploying the right network isolation template, check out the [GitHub Copilot for Azure skill for private networking](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/microsoft-foundry/resource/private-network/private-network.md) set-up!

---

## Overview
This infrastructure-as-code (IaC) solution deploys a network-secured agent environment with private networking and role-based access control (RBAC).

This template covers the full managed VNet deployment path for standard agents, including hosted agents with tool access, required outbound private endpoint rules, and developer access through a jumpbox or VPN.

### Architecture

![Architecture Diagram](./post-deployment-and-diagrams/architecture-diagram.png)

Standard setup supports private network isolation through utilizing **Managed Virtual Network** approach. With Managed Virtual Network, Microsoft manages the virtual network on your behalf, simplifying network configuration while maintaining enterprise-grade isolation.

### Managed VNet Architecture Overview

```text
CUSTOMER SUBSCRIPTION

  Customer VNet (192.168.0.0/16)
  - GatewaySubnet (192.168.2.0/27): VPN Gateway (P2S + Entra ID)
  - PE Subnet (192.168.1.0/24): private endpoints to AI Services, Storage, Cosmos DB, Search, CAE
  - Jumpbox Subnet (192.168.3.0/24): jumpbox VM with managed identity
  - Tools Subnet (192.168.4.0/23): Container App Environment for OpenAPI, MCP, and A2A tools

  Azure AI Foundry Account (publicNetworkAccess: Disabled)
  - AI Services endpoints (account, openai, services.ai)
  - model deployments
  - project capability host
  - system-assigned managed identity for PE approval

MICROSOFT-MANAGED NETWORK

  Managed VNet for hosted agent containers
  - outbound PE rules back to the Foundry account and dependent services
  - support for code interpreter, file search, and tool orchestration
```

### Request Flow for Hosted Agents with Tools

```text
Developer -> VPN/SSH -> Customer VNet -> private endpoint -> Foundry account
Foundry account -> hosted agent container in Microsoft-managed VNet
Hosted agent container -> self private endpoint -> Foundry account for model inference
Hosted agent container -> tools-cae-pe -> Container App Environment for tool calls
```

---

## When to Use This Template

Use this template when you need:
- **Network isolation without managing your own VNet** — Microsoft manages the virtual network infrastructure for you
- **Standard agent setup with BYO resources** — Customer-managed Storage, Cosmos DB, and AI Search for data residency and compliance
- **Simplified networking** — No subnet planning, delegation, or NSG management required
- **System Assigned Managed Identity** — Simplified identity management with platform-managed credentials

### Template Decision Guide

Use the table below to choose the right infrastructure template for your scenario:

| Template | Agent Type | Networking | Identity | Key Use Case |
|----------|-----------|------------|----------|-------------|
| [**15**](../15-private-network-standard-agent-setup/) | Standard (BYO resources) | BYO VNet + Private Endpoints | System Assigned MI | E2E network isolation with full agent capabilities |
| [**19**](../19-private-network-agent-tools/) | Standard (BYO resources) | BYO VNet + Private Endpoints | System Assigned MI | Same as 15 **plus** tools behind VNet (MCP, OpenAPI, Functions, A2A) |
| [**17**](../17-private-network-standard-user-assigned-identity-agent-setup/) | Standard (BYO resources) | BYO VNet + Private Endpoints | **User Assigned MI** | Same as 15 but with user-managed identity |
| [**16**](../16-private-network-standard-agent-apim-setup/) | Standard (BYO resources) | BYO VNet + Private Endpoints | System Assigned MI | Same as 15 **plus** private APIM integration |
| [**18** (this template)](../18-managed-virtual-network/) | Standard (BYO resources) | **Managed VNet** (Microsoft-managed) | System Assigned MI | Network isolation without managing your own VNet |
| [**15a**](../15a-private-network-evaluation-only-setup/) | Evaluation only | BYO VNet + Private Endpoints | System Assigned MI | Minimal setup for evaluation — no Cosmos DB, AI Search, or capability host |
| [**11**](../11-private-network-basic-vnet/) | **Basic** (platform-managed) | BYO VNet injection | System Assigned MI | Basic agents with VNet isolation — no BYO resources needed |
| [**41**](../41-standard-agent-setup/) | Standard (BYO resources) | **Public** (no VNet) | System Assigned MI | Standard agents without network isolation |
| [**40**](../40-basic-agent-setup/) | **Basic** (platform-managed) | **Public** (no VNet) | System Assigned MI | Simplest setup — no BYO resources, no private networking |

### What Gets Deployed

| Resource | Purpose | Public Access |
|----------|---------|:-------------:|
| VNet | Customer network backbone | N/A |
| PE subnet | Private endpoints for Foundry and dependencies | N/A |
| GatewaySubnet | VPN Gateway | N/A |
| Jumpbox subnet + VM | Testing from inside the VNet | Via NSG |
| Tools subnet + Container App Environment | OpenAPI, MCP, and A2A tool hosting | Disabled |
| AI Foundry account | Agent hosting and model inference | Disabled |
| Model deployment | Default model deployment for agents | N/A |
| Storage account | File uploads and code interpreter data | Disabled |
| Cosmos DB | Agent thread and session state | Disabled |
| Azure AI Search | Vector store and file search | Disabled |
| Private DNS zones | Name resolution for private endpoints | N/A |

---

## Deploy to Azure

[![Deploy To Azure](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/deploytoazure.svg?sanitize=true)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fazure-ai-foundry%2Ffoundry-samples%2Frefs%2Fheads%2Fmain%2Finfrastructure%2Finfrastructure-setup-bicep%2F18-managed-virtual-network%2Fazuredeploy.json)

---

## Prerequisites

1. **Active Azure subscription with appropriate permissions**
  - **Foundry Account Owner**: Needed to create the Microsoft Foundry account and project.
  - **Owner or Role Based Access Administrator**: Needed to assign RBAC on the Azure resources used by this template.
  - **Foundry User**: Needed to create and use agents, projects, or evaluation workloads after deployment.

1. Azure CLI installed and configured on your local workstation or deployment pipeline server. Azure CLI support is required to run the 'az rest' commands to update your managed virtual network. 

1. **Register Resource Providers**

   Make sure you have an active Azure subscription that allows registering resource providers. If it's not already registered, run the commands below:

   ```bash
   az provider register --namespace 'Microsoft.KeyVault'
   az provider register --namespace 'Microsoft.CognitiveServices'
   az provider register --namespace 'Microsoft.Storage'
   az provider register --namespace 'Microsoft.Search'
   az provider register --namespace 'Microsoft.Network'
   az provider register --namespace 'Microsoft.ContainerService'
   ```

1. Network administrator permissions (if operating in a restricted or enterprise environment)

1. Sufficient quota for all resources required by this template in the target Azure region, including model deployment quota.
    * If no parameters are passed in, this template creates a Microsoft Foundry resource, Foundry project, Azure Cosmos DB for NoSQL, Azure AI Search, and Azure Storage account

---

## Pre-Deployment Steps

### Limitations / Known Issues

1. You can deploy a managed network Foundry resource in three ways.
   1. Bicep template in this folder
   2. Terraform template in this repository: [`infrastructure-setup-terraform/18-managed-virtual-network`](../../../infrastructure-setup-terraform/18-managed-virtual-network/)
   3. `az rest` and Azure CLI commands `az cognitiveservices` documented in the [`azure-cli/`](azure-cli/) folder in this directory
1. There is no Azure Portal UI support to create the managed network yet. Support is coming soon.
1. Once your Foundry resource is created, ensure you have assigned the Foundry resource's managed identity the built-in role of `Azure AI Enterprise Network Connection Approver` (role ID: `b556d68e-0be0-4f35-a333-ad7ee1ce17ea`) to ensure the required private endpoint to the Foundry resource is created and approved.
1. You can't disable managed virtual network isolation after enabling it. There's no upgrade path from custom virtual network set-up to managed virtual network. A Foundry resource redeployment is required. Deleting your Foundry resource deletes the managed virtual network.
1. Support for managed virtual network is only in the following regions: **East US, East US2, Japan East, France Central, UAE North, Brazil South, Spain Central, Germany West Central, Italy North, South Central US, Australia East, Sweden Central, Canada East, South Africa North, West US, West US 3, South India, and UK South.** Additional region support to follow soon.
1. If you require private access to on-premises resources for your Foundry resource, use Application Gateway to configure on-premises access. The same set-up with a private endpoint to Application Gateway and setting up backend pools is supported. Both L4 and L7 traffic are now supported with the Application Gateway in GA.
1. If you create FQDN outbound rules when the managed virtual network is in **Allow Only Approved Outbound** mode, a managed Azure Firewall is created which comes with associated Firewall costs. The FQDN outbound rules only support ports 80 and 443.
1. You can't bring your own Azure Firewall to the managed virtual network. A managed firewall is automatically created for your Foundry account when you use **Allow Only Approved Outbound** mode.
1. You can't reuse the same managed firewall for multiple Foundry accounts. Each Foundry account creates its own managed firewall when you use **Allow Only Approved Outbound** mode.
1. To ensure your second created project inherits the networking settings of your first project and first Foundry resource, follow the steps in the [network secured Agent README](../15-private-network-standard-agent-setup/README.md) under __# (Optional) Adding Multiple Projects to AI Foundry Deployment__. This is required for new projects added to a Foundry resource seucred with managed network as well. 

### Account Deletion Prerequisites and Cleanup Guidance

Before deleting an **Account** resource, it is essential to first delete the associated **Account Capability Host**. Failure to do so may result in residual dependencies—such as subnets and other provisioned resources—remaining linked to the capability host.

**Cleanup Options**

**1. Full Account Removal**: To completely remove an account, you must delete and purge the account. Simply deleting the account is not sufficient—you must purge so that deletion of the associated capability host is triggered. The service will automatically handle the removal of the capability host and any linked resources in the background. To purge the account, use the following [link](https://learn.microsoft.com/en-us/azure/ai-services/recover-purge-resources?tabs=azure-portal#purge-a-deleted-resource). Please allow approximately max of 20 minutes for all resources to be fully unlinked from the account.

**2. Retain Account, Remove Capability Host**: If you intend to retain the account but remove the capability host, execute the script `deleteCaphost.sh` located in this folder. After deletion, allow approximately max of 20 minutes for all resources to be fully unlinked from the account. To recreate the capability host for the account, use the script `createCaphost.sh` located in the same folder.

> **Important**: Before deleting the account capability host, ensure that the **project capability host** is deleted.

### Template Customization

Note: If not provided, the following resources will be created automatically for you:
- VNet and one subnet
- Azure Cosmos DB for NoSQL  
- Azure AI Search
- Azure Storage

**Optional Integration:** API Management services can be integrated by providing an existing API Management service resource ID.

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
| `vnetName` | Virtual Network name | `agent-vnet-test` | No |
| `peSubnetName` | Subnet name for private endpoints | `pe-subnet` | No |
| `peSubnetPrefix` | Address prefix for PE subnet | `192.168.1.0/24` | No |
| `existingVnetResourceId` | Full ARM Resource ID of an existing VNet | `''` (creates new) | No |
| `vnetAddressPrefix` | Address space for new VNet | `192.168.0.0/16` | No |
| `aiSearchResourceId` | ARM Resource ID of existing AI Search | `''` (creates new) | No |
| `azureStorageAccountResourceId` | ARM Resource ID of existing Storage account | `''` (creates new) | No |
| `azureCosmosDBAccountResourceId` | ARM Resource ID of existing Cosmos DB | `''` (creates new) | No |
| `existingDnsZones` | Map of DNS zone names to resource groups | All empty (creates new) | No |

#### BYO Resource Details

1. **Use Existing Virtual Network and Subnets**

To use an existing VNet and subnet, set the existingVnetResourceId parameter to the full Azure Resource ID of the target VNet and its address range, and provide the names of the required subnet.  If the existing VNet is associated with private DNS zones, set the existingDnsZones parameter to the resource group name in which the zones are located. For example:
- param existingVnetResourceId = "/subscriptions/<subscription-id>/resourceGroups/<resource-group-name>/providers/Microsoft.Network/virtualNetworks/<vnet-name>"
- param peSubnetName string = 'pe-subnet' //optional, default is 'pe-subnet'
- param peSubnetPrefix string = '192.168.1.0/24' //optional, default is '192.168.1.0/24'
- param existingDnsZones = {
       
         'privatelink.services.ai.azure.com': 'privzoneRG' //add resource group name where your private DNS zone is located
       
         'privatelink.openai.azure.com': '' //Leave empty to create new private dns zone... }

💡 If subnets information is provided then make sure it exist within the specified VNet to avoid deployment errors. If subnet information is not provided, the template will create subnets with the default address space.


2. **Use an existing Azure Cosmos DB for NoSQL**

To use an existing Cosmos DB for NoSQL resource, set cosmosDBResourceId parameter to the full Azure Resource ID of the target Cosmos DB.
- param azureCosmosDBAccountResourceId string =  /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmosDbAccountName}

> **⚠️ Important: Cosmos DB Connection Requirements**
>
> When creating the Cosmos DB connection (e.g., via REST API or ARM), ensure the following:
> - The `authType` **must** be set to `AAD`. This is the only supported authentication type for the Cosmos DB connection used by the Agent Service.
> - The `metadata` section **must** include the `ResourceId` property, set to the full Azure Resource ID of your Cosmos DB account. The Agent Service relies on this property to correctly identify and connect to your Cosmos DB resource. Omitting `ResourceId` from the metadata will cause the connection to fail.
>
> Example connection properties:
> ```json
> {
>   "category": "CosmosDB",
>   "authType": "AAD",
>   "metadata": {
>     "ApiType": "Azure",
>     "ResourceId": "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmosDbAccountName}",
>     "location": "{region}"
>   }
> }
> ```


3. **Use an existing Azure AI Search resource**

To use an existing Azure AI Search resource, set aiSearchServiceResourceId parameter to the full Azure resource Id of the target Azure AI Search resource. 
 - param aiSearchResourceId string = /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Search/searchServices/{searchServiceName}


4. **Use an existing Azure Storage account**

To use an existing Azure Storage account, set aiStorageAccountResourceId parameter to the full Azure resource Id of the target Azure Storage account resource. 
- param aiStorageAccountResourceId string = /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}

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

> **Important**: Follow the [Account Deletion Prerequisites and Cleanup Guidance](#account-deletion-prerequisites-and-cleanup-guidance) to properly purge the account and wait for the capability host to fully unlink (~20 minutes).

---

## Post-Deployment Steps (Critical for Hosted Agents)

After deploying the Bicep template, you **must** run the `post-deploy.sh` script to create outbound private endpoint rules in the managed virtual network. Without this step, hosted agents will fail with `500(403: Public access is disabled)`. The templates are located in the folder `post-deployment-and-diagrams`.

```bash
./post-deployment-and-diagrams/post-deploy.sh <resource-group> <ai-services-account-name>
```

This script creates the following outbound PE rules:

| Rule Name | Target Resource | Subresource | Why |
|-----------|----------------|-------------|-----|
| `foundry-account-pe` | AI Services account (self) | `account` | **CRITICAL** — Hosted agent → model calls |
| `storage-pe` | Storage Account | `blob` | File uploads, code interpreter |
| `cosmosdb-pe` | CosmosDB | `Sql` | Thread state persistence |
| `search-pe` | AI Search | `searchService` | Vector store, file search |
| `tools-cae-pe` | Container App Environment | `managedEnvironments` | Agent → tool servers (if tools deployed) |

> **Why is the self-PE needed?** Hosted agent containers run in Microsoft's separate Managed VNet. When the container calls the model (e.g., GPT-4.1), it must traverse a private endpoint to reach the Foundry account. Without the self-PE, traffic attempts the public path, which is blocked by `publicNetworkAccess: Disabled`, resulting in a `403`. Prompt agents don't need this because they execute inside the account itself.

> **Note**: The AI Services managed identity must have the `Contributor` and `Azure AI Enterprise Network Connection Approver` roles at the resource group scope for outbound PE connections to auto-approve. The Bicep template assigns the Network Connection Approver role automatically.

### Additional Helper Scripts

| Script | Purpose |
|--------|--------|
| `setup-jumpbox-access.sh <resource-group>` | Configures jumpbox SSH access: assigns public IP, creates NSG rules for your current IP, assigns MI roles |
| `setup-vpn-client.sh <resource-group>` | Downloads P2S VPN client configuration for developer access via Azure VPN Client with Entra ID auth |

### Manual Execution (if `post-deploy.sh` fails)

```bash
az rest --method put \
  --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}/managednetworks/default/outboundrules/foundry-account-pe?api-version=2025-10-01-preview" \
  --body '{
    "properties": {
      "type": "PrivateEndpoint",
      "category": "UserDefined",
      "destination": {
        "serviceResourceId": "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}",
        "subresourceTarget": "account"
      }
    }
  }'
```

---

## Using Tools with Managed VNet (OpenAPI, MCP, A2A)

Use this template when you need hosted agents to call private tool endpoints through a Container App Environment.

### Why Hosted Agents Need the Self Private Endpoint

When `publicNetworkAccess: Disabled`, both of these networks need private access to the Foundry account:

1. Your VNet, which is handled by the customer-managed private endpoint.
2. The Microsoft-managed VNet used by hosted agent containers, which requires the self private endpoint `foundry-account-pe`.

Prompt agents do not need this extra hop because they execute inside the Foundry account infrastructure. Hosted agents do, because they run in a separate Microsoft-managed network and must call back into the account for model inference.

| Aspect | Prompt Agent / Toolbox | Hosted Agent |
|--------|------------------------|--------------|
| Execution location | Inside the Foundry account infrastructure | In a container in Microsoft's managed VNet |
| Needs customer VNet PE | Yes | Yes |
| Needs self-PE | No | Yes |
| Failure mode when missing | N/A | `500` wrapping `403: Public access is disabled` |

### Recommended Deployment Sequence for Tools

1. Deploy the Bicep template.
2. Run `./post-deploy.sh <resource-group> <ai-services-account-name>`.
3. Configure jumpbox access with `./setup-jumpbox-access.sh <resource-group> --install-tools`.
4. Optionally configure VPN access with `./setup-vpn-client.sh <resource-group>`.
5. Deploy and validate your OpenAPI, MCP, or A2A tool servers behind the Container App Environment private endpoint.

### Developer Access

#### Option A: Jumpbox

```bash
./setup-jumpbox-access.sh <your-rg>
ssh azureuser@<jumpbox-public-ip>
```

#### Option B: P2S VPN

```bash
./setup-vpn-client.sh <your-rg>
```

Import `azurevpnconfig.xml` into Azure VPN Client and connect with Entra ID.

#### Option C: ACI in the VNet

If you test from Azure Container Instances deployed into the same VNet or a peered VNet, connectivity follows the same private endpoint path as the jumpbox.

### Testing Agents

```bash
# Get token via IMDS from the jumpbox
TOKEN=$(curl -s 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://cognitiveservices.azure.com' -H 'Metadata: true' | jq -r '.access_token')

# Prompt agent should work without the self-PE
curl -X POST "https://<account>.services.ai.azure.com/api/projects/<project>/agents/<prompt-agent>/endpoint/protocols/openai/responses?api-version=2025-11-15-preview" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"input": "What is 2+2?"}'

# Hosted agent requires the self-PE
curl -X POST "https://<account>.services.ai.azure.com/api/projects/<project>/agents/<hosted-agent>/endpoint/protocols/openai/responses?api-version=2025-11-15-preview" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"input": "Calculate the square root of 144"}'
```

```bash
# DNS should resolve to a private IP and HTTP should return 401, not timeout
nslookup <account>.services.ai.azure.com
nslookup <account>.openai.azure.com
curl -s -o /dev/null -w '%{http_code}' "https://<account>.services.ai.azure.com"
```

---

## Template 18 vs Template 19 — Key Differences

| Aspect | Template 18 (Managed VNet) | Template 19 (Customer VNet Injection) |
|--------|---------------------------|---------------------------------------|
| **Network Model** | Microsoft manages an opaque VNet for agent compute | Agent compute runs in YOUR VNet (you control it) |
| **VNet Visibility** | Cannot see/manage the managed VNet | Full visibility and control over all subnets |
| **Agent Container Networking** | Outbound via managed VNet PE rules | Direct VNet routing (no special PE rules needed) |
| **Self-PE Required?** | Yes — agent container needs PE back to account | No — container is already in same network as customer PE |
| **Complexity** | Simpler infra, complex post-deploy | Complex infra, simpler post-deploy |
| **Tool Access** | Via outbound PE rule to CAE | Via direct VNet routing or service endpoints |
| **PE Auto-Approval** | Requires managed identity with Network Connection Approver role | Not needed |
| **Known Issue** | Self-PE is not auto-created reliably, so run `post-deploy.sh` | No equivalent issue |
| **Use Case** | Simpler setups, managed experience | Maximum control, compliance, custom DNS |

---  

## Managed Network Secured Agent Project Architecture Deep Dive

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

Network Security
- Public network access disabled
- Private endpoints for all services
- Network ACLs with deny by default

**Network Infrastructure**
- A Virtual Network (192.168.0.0/16) is created (if existing isn't passed in)
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
    - Cosmos DB Built-in Data Contributor (`00000000-0000-0000-0000-000000000002`)
    - Three containers will automatically be provisioned during the create capability host process:
      - Cosmos DB for NoSQL container: `<${projectWorkspaceId}>-thread-message-store`
      - Cosmos DB for NoSQL container: `<${projectWorkspaceId}>-system-thread-message-store`
      - Cosmos DB for NoSQL container: `<${projectWorkspaceId}>-agent-entity-store`
  - **Microsoft Foundry Resource**
    - Azure AI Enterprise Network Connection Approver (`b556d68e-0be0-4f35-a333-ad7ee1ce17ea`) Required role for the Foundry account to accept all private endpoints created in the managed VNET.
---

## Module Structure

```text
modules-network-secured/
├── add-project-capability-host.bicep               # Configuring the project's capability host
├── ai-account-identity.bicep                       # Microsoft Foundry deployment and configuration
├── ai-project-identity.bicep                       # Foundry project deployment and connection configuration           
├── ai-search-role-assignments.bicep                # AI Search RBAC configuration
├── azure-storage-account-role-assignment.bicep     # Storage Account RBAC configuration  
├── blob-storage-container-role-assignments.bicep   # Blob Storage Container RBAC configuration
├── cosmos-container-role-assignments.bicep         # CosmosDB container Account RBAC configuration
├── cosmosdb-account-role-assignment.bicep          # CosmosDB Account RBAC configuration
├── existing-vnet.bicep                             # Bring your existing virtual network to template deployment
├── format-project-workspace-id.bicep               # Formatting the project workspace ID
├── managed-network.bicep                           # Managed virtual network and outbound rules configuration
├── network-agent-vnet.bicep                        # Logic for routing virtual network set-up if existing virtual network is selected
├── private-endpoint-and-dns.bicep                  # Creating private endpoints and DNS zones for dependent resources
├── standard-dependent-resources.bicep              # Deploying CosmosDB, Storage, and Search
├── subnet.bicep                                    # Setting the subnet for Agent network injection
├── validate-existing-resources.bicep               # Validate existing CosmosDB, Storage, and Search to template deployment
└── vnet.bicep                                      # Deploying a new virtual network

azure-cli/
├── azure-cli.md                                    # End-to-end CLI walkthrough for deploying managed VNet via Azure CLI
└── outbound-rules-az-rest.md                       # az rest commands for creating outbound rules (Private Endpoint, FQDN, Service Tag)
```

---

## Deploy via Azure CLI

As an alternative to the Bicep template, you can deploy a managed virtual network entirely using Azure CLI commands. The [`azure-cli/`](azure-cli/) folder contains step-by-step documentation for this approach.

### Files

| File | Description |
|------|-------------|
| [`azure-cli.md`](azure-cli/azure-cli.md) | Complete end-to-end walkthrough covering account creation with network injections, RBAC role assignment, managed network creation, outbound rule configuration, and deployment verification — all using `az cognitiveservices` CLI commands (requires Azure CLI 2.86.0+). |
| [`outbound-rules-az-rest.md`](azure-cli/outbound-rules-az-rest.md) | `az rest` commands for creating individual and batch outbound rules (Private Endpoint rules for Storage, Cosmos DB, and AI Search). Use these when you need direct REST API access or are on an older CLI version. |

### When to use the CLI approach

- You want to add a managed network to an **existing** Foundry resource that was created without one
- You prefer imperative CLI commands over declarative Bicep/Terraform templates
- You need to manage outbound rules independently from the initial infrastructure deployment
- You want to quickly test or prototype managed network configurations

> **Note:** The `az cognitiveservices account managed-network` commands require **Azure CLI 2.86.0 or later**. The commands are currently in preview. For older CLI versions, use the `az rest` equivalents documented in [`outbound-rules-az-rest.md`](azure-cli/outbound-rules-az-rest.md).

## Maintenance

### Regular Tasks

1. Review role assignments
2. Monitor network security
3. Check service health
4. Update configurations as needed

### Troubleshooting

#### Hosted Agent Returns 500 (wrapping 403: "Public access is disabled")

**Root Cause:** The managed VNet self-PE (`foundry-account-pe`) is missing. The hosted agent container cannot reach the Foundry account privately.

**Fix:**
```bash
./post-deploy.sh <resource-group> <ai-services-name>
```

#### Outbound PE Rule Stuck in "Provisioning" or "Failed"

**Root Cause:** The AI Services managed identity lacks the required roles to auto-approve PE connections on target resources.

**Fix:**
```bash
# Get the AI Services MI principal ID
MI_PRINCIPAL=$(az cognitiveservices account show -g <rg> -n <account> --query identity.principalId -o tsv)

# Assign Contributor at RG scope
az role assignment create --assignee "$MI_PRINCIPAL" --role "Contributor" --scope "/subscriptions/{sub}/resourceGroups/{rg}"

# Assign Network Connection Approver
az role assignment create --assignee "$MI_PRINCIPAL" --role "b556d68e-0be0-4f35-a333-ad7ee1ce17ea" --scope "/subscriptions/{sub}/resourceGroups/{rg}"
```

Then re-run `./post-deploy.sh`.

#### Cannot SSH to Jumpbox (Connection Timeout)

**Possible causes:** no public IP on the jumpbox NIC, NSG rules blocking your current IP, or your public IP changed.

```bash
./setup-jumpbox-access.sh <resource-group>

MY_IP=$(curl -s https://api.ipify.org)/32
az network nsg rule update -g <rg> --nsg-name jumpboxNSG -n AllowSSH-JIT \
  --source-address-prefixes "$MY_IP"
```

```bash
az network nic show -g <rg> -n jumpbox-nic --query "networkSecurityGroup.id" -o tsv
az network vnet subnet show -g <rg> --vnet-name my-vnet -n jumpbox-subnet --query "networkSecurityGroup.id" -o tsv
```

#### DNS Resolution Returns Public IP (Not Private)

**Root Cause:** Private DNS zone not linked to VNet, or DNS zone group not configured on PE.

**Fix:**
```bash
az network private-dns zone list -g <rg> --query "[].name" -o tsv
az network private-dns link vnet list -g <rg> -z privatelink.services.ai.azure.com --query "[].name" -o tsv
az network private-endpoint dns-zone-group list -g <rg> --endpoint-name <pe-name> -o table
```

#### Tool Server Returns 502 or Connection Refused from Agent

**Root Cause:** The `tools-cae-pe` outbound rule is missing or the Container App Environment private endpoint connection is not approved.

```bash
az rest --method get \
  --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}/managednetworks/default/outboundrules/tools-cae-pe?api-version=2025-10-01-preview" \
  --query "properties.{status:status, destination:destination}" -o json

az rest --method get \
  --url "https://management.azure.com{cae-resource-id}/privateEndpointConnections?api-version=2024-10-02-preview" \
  --query "value[].{name:name, status:properties.privateLinkServiceConnectionState.status}" -o table
```

Container App Environment must use `internal: false` with `publicNetworkAccess: Disabled` so hosted agents can reach it over private endpoints.

#### VPN Gateway Not Provisioned Yet

**Root Cause:** VPN Gateway deployment can continue after the main deployment completes.

```bash
az network vnet-gateway show -g <rg> -n vpn-gateway --query provisioningState -o tsv
az deployment group show -g <rg> -n <deployment-name> --query "properties.error" -o json
```

#### Managed Identity Token Acquisition Fails on Jumpbox

**Root Cause:** The jumpbox VM does not have a system-assigned managed identity enabled.

```bash
az vm identity assign -g <rg> -n jumpbox
```

#### PE Connection Shows Pending

**Root Cause:** The managed identity does not have approval rights on the target resource.

```bash
az network private-endpoint-connection list --id <target-resource-id> --query "[?properties.privateLinkServiceConnectionState.status=='Pending']"
az network private-endpoint-connection approve --id <pe-connection-id> --description "Approved for managed VNet"
```

#### `defaultAction: Allow` Was Left Enabled

**Root Cause:** Public access was enabled as a workaround during debugging.

```bash
az cognitiveservices account update -g <rg> -n <account> \
  --custom-domain <account> \
  --api-properties "{'networkAcls':{'defaultAction':'Deny'}}"
```

#### Prompt Agent Works but Hosted Agent Doesn't

This is the canonical symptom of the missing self-PE. Prompt agents execute inside the Foundry account's own infrastructure (no outbound network traversal). Hosted agents run in a separate managed VNet and need the self-PE to reach back to the account.

```bash
# Check PE connection count (expect ≥2: customer VNet PE + self-PE)
az rest --method get \
  --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}?api-version=2025-06-01" \
  --query "properties.privateEndpointConnections | length(@)" -o tsv
```

#### Diagnostic Commands Cheat Sheet

```bash
# All outbound rules in managed network
az rest --method get \
  --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}/managednetworks/default?api-version=2025-10-01-preview" \
  --query "properties.outboundRules"

# All PE connections on the account
az rest --method get \
  --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}?api-version=2025-06-01" \
  --query "properties.privateEndpointConnections[].{name:name, status:properties.privateLinkServiceConnectionState.status, pe:properties.privateEndpoint.id}"

# Account network settings
az cognitiveservices account show -g <rg> -n <account> \
  --query "{publicAccess:properties.publicNetworkAccess, defaultAction:properties.networkAcls.defaultAction}" -o json

# Managed network provisioning state
az rest --method get \
  --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{account}/managednetworks/default?api-version=2025-10-01-preview" \
  --query "properties.provisioningState" -o tsv
```

### Files in This Template

| File | Purpose |
|------|---------|
| `main.bicep` | Full infrastructure deployment for the managed VNet pattern |
| `main.bicepparam` | Parameter file for names and resource configuration |
| `post-deploy.sh` | Creates required outbound private endpoint rules in the managed VNet |
| `setup-jumpbox-access.sh` | Configures jumpbox access, NSG rules, and managed identity setup |
| `setup-vpn-client.sh` | Downloads and explains P2S VPN client configuration |
| `create_search_index.py` | Helper to create the AI Search index for file search capability |
| `architecture-diagram.png` | Visual architecture diagram |
| `README.md` | Canonical guide for this template |

### Quick Reference: Required Outbound PE Rules

| Rule Name | Target Resource | Subresource | Why |
|-----------|----------------|-------------|-----|
| `foundry-account-pe` | AI Services account (self) | `account` | Hosted agent to model calls |
| `storage-pe` | Storage account | `blob` | File uploads and code interpreter |
| `cosmosdb-pe` | Cosmos DB | `Sql` | Thread state persistence |
| `search-pe` | Azure AI Search | `searchService` | Vector store and file search |
| `tools-cae-pe` | Container App Environment | `managedEnvironments` | Hosted agent to tool server communication |

---

## References

- [Configure managed virtual network for Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/how-to/managed-virtual-network)
- [Microsoft Foundry Networking Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link?tabs=azure-portal&pivots=fdp-project)
- [Microsoft Foundry RBAC Documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/rbac-azure-ai-foundry?pivots=fdp-project)
- [Private Endpoint Documentation](https://learn.microsoft.com/en-us/azure/private-link/)
- [RBAC Documentation](https://learn.microsoft.com/en-us/azure/role-based-access-control/)
- [Network Security Best Practices](https://learn.microsoft.com/en-us/azure/security/fundamentals/network-best-practices)
