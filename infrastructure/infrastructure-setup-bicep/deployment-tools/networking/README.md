---
description: Shared Private Link (SPL) setup from Azure AI Search to AI Services / Foundry for private network scenarios.
page_type: tool
products:
- azure
- azure-resource-manager
languages:
- bicep
---

# AI Search → AI Services Shared Private Link

## Overview

When Azure AI Services (the Foundry account) has `publicNetworkAccess=Disabled`, Azure AI Search's vectorizer, indexer enrichment skills, and hosted model skills fail because those calls originate from AI Search's **managed backend infrastructure** — outside your VNet.

Private endpoints in your VNet only cover **inbound** traffic to AI Services. They do nothing for **outbound** calls from AI Search's managed infrastructure.

A **Shared Private Link (SPL)** provisions a private endpoint **from** AI Search's managed infrastructure **into** AI Services via Azure Private Link — no public access required.

---

## What Gets Created

This template deploys **three** SPL resources from AI Search into a single AI Services account. All three are required for full Foundry coverage:

| SPL Name | Group ID | Purpose |
|----------|----------|---------|
| `<prefix>-openai` | `openai_account` | Vectorizer — query-time embedding via integrated vectorization |
| `<prefix>-cogsvc` | `cognitiveservices_account` | Built-in AI enrichment skills (OCR, entity extraction, key phrases) and Foundry billing link |
| `<prefix>-foundry` | `foundry_account` | Azure-hosted model skills — GenAI prompt skill, Azure OpenAI embedding skill, Content Understanding skill |

> **Important**: The standard private endpoint `groupId` value `account` does **not** work for SPLs. Using it returns: `BadRequest: Cannot create private endpoint for requested type 'account'`.

> **Not needed for**: Cosmos DB and Storage — they are passive data stores and never initiate outbound calls from AI Search.

---

## Prerequisites

1. **Azure AI Search** — S1 or higher recommended.
2. **Azure AI Services / Foundry account** already deployed with `publicNetworkAccess=Disabled` (or about to be disabled).
3. Both resources must be in the same Azure subscription for SPL creation.
4. **Permissions**:
   - `Microsoft.Search/searchServices/sharedPrivateLinkResources/write` on the AI Search resource.
   - `Microsoft.CognitiveServices/accounts/privateEndpointConnections/write` on the AI Services resource (for approval).

---

## Parameters

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `aiSearchName` | Name of the Azure AI Search service | — | Yes |
| `aiServicesResourceId` | Full ARM resource ID of the AI Services / Foundry account | — | Yes |
| `splNamePrefix` | Prefix for SPL resource names. Change if connecting multiple AI Services accounts to the same AI Search instance. | `foundry-spl` | No |

---

## Usage

### 1. Fill in the parameter file

Edit `ai-search-shared-private-link.bicepparam`:

```bicep
using './ai-search-shared-private-link.bicep'

param aiSearchName = 'my-ai-search'
param aiServicesResourceId = '/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<foundry-account>'

// Uncomment to customize the SPL name prefix (e.g., when targeting multiple AI Services accounts):
// param splNamePrefix = 'team2-spl'
```

### 2. Deploy

```bash
az deployment group create \
  --resource-group <rg-where-ai-search-lives> \
  --template-file ai-search-shared-private-link.bicep \
  --parameters ai-search-shared-private-link.bicepparam
```

### 3. Approve the pending private endpoint connections

After deployment, the three SPLs are in **Pending** state. They must be approved on the AI Services side before traffic can flow.

**Option A — Azure Portal**:
Navigate to **AI Services resource → Networking → Private endpoint connections** and approve each pending connection.

**Option B — Azure CLI**:
```bash
# List pending connections
az network private-endpoint-connection list \
  --id /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<name>

# Approve each connection
az network private-endpoint-connection approve \
  --id <connection-resource-id> \
  --description "Approved for AI Search SPL"
```

> **Note**: The SPLs will not route traffic until approved. Approval is a one-time step per SPL.

---

## Redeployment

Re-running the deployment is safe — ARM PUT operations are idempotent. Existing SPLs are updated in place. However, if a connection was previously **rejected**, re-deployment creates a new pending connection that must be approved again.

---

## Limitations

### How the templates connect AI Search → AI Services today

The private network templates ([15](../../infrastructure-setup-bicep/15-private-network-standard-agent-setup/), [16](../../infrastructure-setup-bicep/16-private-network-standard-agent-apim-setup/), [17](../../infrastructure-setup-bicep/17-private-network-standard-user-assigned-identity-agent-setup/), [18](../../infrastructure-setup-bicep/18-managed-virtual-network/), [19](../../infrastructure-setup-bicep/19-private-network-agent-tools/)) deploy:

| Resource | Setting | Template Value |
|----------|---------|---------------|
| AI Services (Foundry) | `publicNetworkAccess` | `Disabled` |
| AI Services (Foundry) | `networkAcls.bypass` | `AzureServices` (trusted services bypass **on**) |
| AI Services (Foundry) | `networkAcls.defaultAction` | `Deny` |
| AI Search | SKU | `standard` (S1) |
| AI Search | `publicNetworkAccess` | `disabled` |
| AI Search | `networkRuleSet.bypass` | `None` |
| AI Search | SPLs | **None deployed** |

**Default behavior**: AI Search reaches AI Services via the **trusted services bypass** (`bypass: 'AzureServices'`). `Microsoft.Search` is on the [trusted services list](https://learn.microsoft.com/en-us/azure/ai-services/cognitive-services-virtual-networks#grant-access-to-trusted-azure-services-for-azure-openai), so AI Search's managed infrastructure can connect without SPLs.

**With this tool**: Deploy SPLs and then set AI Services `bypass` to `'None'` for zero-trust — only your specific AI Search instance has private access.

### SPL operational constraints

Per [SPL documentation](https://learn.microsoft.com/en-us/azure/search/search-indexer-howto-access-private) and [service limits](https://learn.microsoft.com/en-us/azure/search/search-limits-quotas-capacity#shared-private-link-resource-limits):

| Constraint | Details | Source |
|------------|---------|--------|
| **`openai_account` — public cloud + Azure Gov only** | Other sovereign clouds do not support `openai_account` SPLs. | [SPL docs footnote 7](https://learn.microsoft.com/en-us/azure/search/search-indexer-howto-access-private#prerequisites) |
| **One SPL per resource + groupId** | Only one SPL can exist per resource and subresource (`groupId`) combination on a search service. | [SPL docs](https://learn.microsoft.com/en-us/azure/search/search-indexer-howto-access-private#when-to-use-a-shared-private-link) |
| **Billed feature** | Shared private links are billed through [Azure Private Link pricing](https://azure.microsoft.com/pricing/details/private-link/). | [SPL docs](https://learn.microsoft.com/en-us/azure/search/search-indexer-howto-access-private) |

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `BadRequest: Cannot create private endpoint for requested type 'account'` | Wrong `groupId` — used `account` instead of the SPL-specific values | Use `openai_account`, `cognitiveservices_account`, or `foundry_account` |
| SPL stuck in **Pending** state | Connection not approved on AI Services side | Approve the connection (see step 3 above) |
| SPL shows **Rejected** | Someone rejected the connection on AI Services side | Delete the SPL, redeploy, and approve the new connection |
| Vectorizer still fails after SPL approval | DNS resolution not updated yet | Wait a few minutes for DNS propagation, or verify the private DNS zone for `*.openai.azure.com` is linked to your VNet |
| `Conflict` error during deployment | AI Search only accepts one SPL write at a time; parallel writes conflict | The template handles this via `dependsOn` — if running manually, serialize the operations |

---
