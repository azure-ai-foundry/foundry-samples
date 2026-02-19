# Foundry IQ KB MCP Server — VNet Enterprise Testing Guide

This guide walks through testing the **Foundry IQ Knowledge Base MCP server** with Azure AI Foundry Agent Service V2 in a VNet / private endpoint scenario.

The Foundry IQ KB MCP server (`@foundry-iq/kb-mcp-app`) exposes the `knowledge_base_retrieve` tool via MCP, which queries Azure AI Search's Knowledge Base API (`2025-11-01-preview`). In this VNet scenario, the MCP server runs as a Container App inside the VNet and connects to a private AI Search endpoint.

---

## Why Containerization Is Required for VNet Scenarios

A common question is whether customers can simply use the existing hosted MCP server (e.g., `foundry-iq-mcp-apps.vercel.app/mcp`) instead of deploying their own container. **The answer is no — if AI Search is behind a VNet with public access disabled, the MCP server must run inside the VNet.**

Here's why:

1. **The MCP server makes direct HTTP calls to AI Search.** The `search-client.ts` `liveRetrieve()` method uses `fetch()` to call `{AZURE_SEARCH_ENDPOINT}/knowledgebases/{name}/retrieve`. This requires network-level access to the AI Search endpoint.

2. **A hosted MCP server on Vercel/public internet cannot reach private AI Search endpoints.** Private endpoints are only resolvable and accessible from within the VNet.

3. **The Agent Service's Data Proxy uses `networkInjection` (via the Capability Host's `customerSubnet`) to reach into the VNet.** When the agent calls the MCP tool, the Data Proxy routes the request to the MCP server. If the MCP server is inside the VNet (Container App with internal ingress), the Data Proxy can reach it.

### Customer Options

| Option | MCP Server | AI Search Access | Security Level |
|--------|-----------|------------------|----------------|
| **A: Containerize MCP in VNet** | Container App on `mcp-subnet` | Private EP only | ★★★ Full isolation |
| **B: Use built-in AI Search tool** | None (use `AzureAISearchAgentTool`) | Via Data Proxy connection | ★★★ Full isolation |
| **C: Keep AI Search public** | Hosted (Vercel) or any | Public + API key | ★☆☆ AI Search exposed |

**This guide covers Option A** — the recommended approach for enterprise VNet scenarios requiring both KB API features and full network isolation.

---

## Table of Contents

1. [Architecture](#architecture)
2. [Prerequisites](#prerequisites)
3. [Step 1: Deploy Template 19 Infrastructure](#step-1-deploy-template-19-infrastructure)
4. [Step 2: Set Up Private AI Search Endpoint](#step-2-set-up-private-ai-search-endpoint)
5. [Step 3: Create Sample Knowledge Base](#step-3-create-sample-knowledge-base)
6. [Step 4: Build and Deploy the Foundry IQ KB MCP Server](#step-4-build-and-deploy-the-foundry-iq-kb-mcp-server)
7. [Step 5: Configure Private DNS](#step-5-configure-private-dns)
8. [Step 6: Run Connectivity Tests](#step-6-run-connectivity-tests)
9. [Step 7: Run Agent Integration Tests](#step-7-run-agent-integration-tests)
10. [Step 8: Validate SharePoint Header Passthrough](#step-8-validate-sharepoint-header-passthrough)
11. [Troubleshooting](#troubleshooting)
12. [Test Results Summary](#test-results-summary)

---

## Architecture

```
┌───────────────────────────────────────────────────────────────┐
│  Test Client (python test_foundry_iq_kb_mcp.py)               │
└──────────────────────────┬────────────────────────────────────┘
                           │ Responses API (HTTPS)
              ┌────────────▼────────────┐
              │  AI Foundry Agent V2     │
              │  MCPTool configured      │
              │  (Public or Private)     │
              └────────────┬────────────┘
                           │ Data Proxy / networkInjection
              ┌────────────▼────────────────────────────────────┐
              │  Private VNet                                    │
              │                                                  │
              │  ┌───────────────────────┐                       │
              │  │ Foundry IQ KB MCP     │  Container App        │
              │  │ Server                │  (internal ingress)   │
              │  │ /mcp endpoint         │  Port 8080            │
              │  │ knowledge_base_retrieve│                      │
              │  └───────────┬───────────┘                       │
              │              │ KB API (2025-11-01-preview)        │
              │  ┌───────────▼───────────┐                       │
              │  │ Azure AI Search       │  Private Endpoint      │
              │  │ /knowledgebases/      │                        │
              │  │   {name}/retrieve     │                        │
              │  │                       │                        │
              │  │ Sources:              │                        │
              │  │  • SharePoint (SP)    │  x-ms-sharepoint-*     │
              │  │  • Search Index       │  headers forwarded     │
              │  │  • Web                │                        │
              │  └───────────────────────┘                       │
              └──────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure CLI installed and authenticated (`az login`)
- Owner or Contributor role on the subscription
- Python 3.10+ (for test scripts)
- Docker (for building the MCP server container image)
- Node.js 22+ (for building the MCP server from source, optional)

### Python dependencies

```bash
pip install azure-ai-projects azure-identity openai
```

---

## Step 1: Deploy Template 19 Infrastructure

Template 19 ("Hybrid Private Resources Agent Setup") creates:
- VNet with subnets (agent, private endpoint, MCP)
- AI Services account with model deployment
- AI Search, Cosmos DB, Storage — all on private endpoints
- Project with capability host and connections

```bash
RESOURCE_GROUP="rg-foundry-iq-kb-vnet-test"
LOCATION="westus2"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Deploy infrastructure
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters location=$LOCATION

# Capture outputs
AI_SERVICES_NAME=$(az cognitiveservices account list -g $RESOURCE_GROUP --query "[0].name" -o tsv)
AI_SEARCH_NAME=$(az search service list -g $RESOURCE_GROUP --query "[0].name" -o tsv)
VNET_NAME=$(az network vnet list -g $RESOURCE_GROUP --query "[0].name" -o tsv)
echo "AI Services: $AI_SERVICES_NAME"
echo "AI Search: $AI_SEARCH_NAME"
echo "VNet: $VNET_NAME"
```

See [../README.md](../README.md) for full deployment details.

---

## Step 2: Set Up Private AI Search Endpoint

Template 19 automatically creates AI Search with a private endpoint. Verify:

```bash
# List private endpoints — should include *search-private-endpoint
az network private-endpoint list -g $RESOURCE_GROUP -o table

# Verify AI Search has public access disabled
az search service show -g $RESOURCE_GROUP -n $AI_SEARCH_NAME \
  --query "publicNetworkAccess" -o tsv
# Expected: disabled
```

### VNet AI Search — Detailed Setup (if creating manually)

If you need to create a separate AI Search resource behind a VNet:

```bash
# 1. Create AI Search with public access disabled
az search service create \
  --name "my-private-search" \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku standard \
  --public-network-access disabled

# 2. Create private endpoint in PE subnet
PE_SUBNET_ID=$(az network vnet subnet show \
  -g $RESOURCE_GROUP --vnet-name $VNET_NAME -n "pe-subnet" --query "id" -o tsv)

SEARCH_ID=$(az search service show \
  -g $RESOURCE_GROUP -n "my-private-search" --query "id" -o tsv)

az network private-endpoint create \
  --name "search-private-endpoint" \
  --resource-group $RESOURCE_GROUP \
  --vnet-name $VNET_NAME \
  --subnet "pe-subnet" \
  --private-connection-resource-id $SEARCH_ID \
  --group-ids searchService \
  --connection-name "search-pe-connection"

# 3. Create private DNS zone
az network private-dns zone create \
  --resource-group $RESOURCE_GROUP \
  --name "privatelink.search.windows.net"

# 4. Link DNS zone to VNet
VNET_ID=$(az network vnet show -g $RESOURCE_GROUP -n $VNET_NAME --query "id" -o tsv)
az network private-dns link vnet create \
  --resource-group $RESOURCE_GROUP \
  --zone-name "privatelink.search.windows.net" \
  --name "search-dns-link" \
  --virtual-network $VNET_ID \
  --registration-enabled false

# 5. Create DNS records for the private endpoint
PE_NIC_ID=$(az network private-endpoint show \
  -g $RESOURCE_GROUP -n "search-private-endpoint" \
  --query "networkInterfaces[0].id" -o tsv)

PE_IP=$(az network nic show --ids $PE_NIC_ID \
  --query "ipConfigurations[0].privateIpAddress" -o tsv)

az network private-dns record-set a add-record \
  --resource-group $RESOURCE_GROUP \
  --zone-name "privatelink.search.windows.net" \
  --record-set-name "my-private-search" \
  --ipv4-address $PE_IP

# 6. Verify private connectivity (from within VNet or VPN)
nslookup my-private-search.search.windows.net
# Should resolve to the private IP
```

---

## Step 3: Create Sample Knowledge Base

The sample KB contains 12 documents across 3 source types: SharePoint (4), search index (4), and web (4).

```bash
# Get admin key
ADMIN_KEY=$(az search admin-key show \
  -g $RESOURCE_GROUP --service-name $AI_SEARCH_NAME \
  --query "primaryKey" -o tsv)

SEARCH_ENDPOINT="https://${AI_SEARCH_NAME}.search.windows.net"

# Temporarily enable public access for data seeding
python create_sample_kb.py \
  --endpoint $SEARCH_ENDPOINT \
  --api-key $ADMIN_KEY \
  --kb-name test-kb \
  --toggle-public-access \
  --resource-group $RESOURCE_GROUP \
  --search-service-name $AI_SEARCH_NAME
```

If you're on the VNet (VPN/Bastion), you can skip `--toggle-public-access`:

```bash
python create_sample_kb.py \
  --endpoint $SEARCH_ENDPOINT \
  --api-key $ADMIN_KEY \
  --kb-name test-kb
```

---

## Step 4: Build and Deploy the Foundry IQ KB MCP Server

### 4.1 Build the container image

```bash
# Clone the foundry-iq-mcp-apps source
# (or copy the source into the foundry-iq-kb-mcp/ directory)
cd foundry-iq-kb-mcp/

# Copy source files
cp -r /path/to/foundry-iq-mcp-apps/package.json .
cp -r /path/to/foundry-iq-mcp-apps/package-lock.json .
cp -r /path/to/foundry-iq-mcp-apps/tsconfig.json .
cp -r /path/to/foundry-iq-mcp-apps/tsconfig.server.json .
cp -r /path/to/foundry-iq-mcp-apps/vite.config.ts .
cp -r /path/to/foundry-iq-mcp-apps/scripts/ .
cp -r /path/to/foundry-iq-mcp-apps/src/ .
cp -r /path/to/foundry-iq-mcp-apps/public/ .

# Build with Docker
docker build -t foundry-iq-kb-mcp:latest .
```

### 4.2 Push to Azure Container Registry

```bash
# Create ACR
ACR_NAME="kbmcpacr$(date +%s | tail -c 5)"
az acr create --name $ACR_NAME -g $RESOURCE_GROUP --sku Basic --location $LOCATION

# Login and push
az acr login --name $ACR_NAME
docker tag foundry-iq-kb-mcp:latest ${ACR_NAME}.azurecr.io/foundry-iq-kb-mcp:latest
docker push ${ACR_NAME}.azurecr.io/foundry-iq-kb-mcp:latest

# Create identity with AcrPull
az identity create --name kb-mcp-identity -g $RESOURCE_GROUP --location $LOCATION
IDENTITY_ID=$(az identity show --name kb-mcp-identity -g $RESOURCE_GROUP --query "id" -o tsv)
IDENTITY_PRINCIPAL=$(az identity show --name kb-mcp-identity -g $RESOURCE_GROUP --query "principalId" -o tsv)
ACR_ID=$(az acr show --name $ACR_NAME --query "id" -o tsv)
az role assignment create --assignee $IDENTITY_PRINCIPAL --role AcrPull --scope $ACR_ID
sleep 30
```

### 4.3 Deploy to Container Apps (internal VNet)

```bash
MCP_SUBNET_ID=$(az network vnet subnet show \
  -g $RESOURCE_GROUP --vnet-name $VNET_NAME -n "mcp-subnet" --query "id" -o tsv)

# Create internal Container Apps environment
az containerapp env create \
  --resource-group $RESOURCE_GROUP \
  --name "kb-mcp-env" \
  --location $LOCATION \
  --infrastructure-subnet-resource-id $MCP_SUBNET_ID \
  --internal-only true

# Deploy the Foundry IQ KB MCP server
az containerapp create \
  --resource-group $RESOURCE_GROUP \
  --name "foundry-iq-kb-mcp" \
  --environment "kb-mcp-env" \
  --image "${ACR_NAME}.azurecr.io/foundry-iq-kb-mcp:latest" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --user-assigned $IDENTITY_ID \
  --registry-server "${ACR_NAME}.azurecr.io" \
  --registry-identity $IDENTITY_ID \
  --env-vars \
    AZURE_SEARCH_ENDPOINT=$SEARCH_ENDPOINT \
    AZURE_SEARCH_API_KEY=$ADMIN_KEY \
    AZURE_SEARCH_KB_NAME=test-kb

# Get the MCP server URL
MCP_FQDN=$(az containerapp show -g $RESOURCE_GROUP -n "foundry-iq-kb-mcp" \
  --query "properties.configuration.ingress.fqdn" -o tsv)
echo "KB MCP Server URL: https://${MCP_FQDN}/mcp"
```

### 4.4 (Optional) Deploy public instance for testing

```bash
az containerapp env create \
  --resource-group $RESOURCE_GROUP \
  --name "kb-mcp-env-public" \
  --location $LOCATION

az containerapp create \
  --resource-group $RESOURCE_GROUP \
  --name "foundry-iq-kb-mcp-public" \
  --environment "kb-mcp-env-public" \
  --image "${ACR_NAME}.azurecr.io/foundry-iq-kb-mcp:latest" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --user-assigned $IDENTITY_ID \
  --registry-server "${ACR_NAME}.azurecr.io" \
  --registry-identity $IDENTITY_ID \
  --env-vars \
    AZURE_SEARCH_ENDPOINT=$SEARCH_ENDPOINT \
    AZURE_SEARCH_API_KEY=$ADMIN_KEY \
    AZURE_SEARCH_KB_NAME=test-kb

PUBLIC_MCP_FQDN=$(az containerapp show -g $RESOURCE_GROUP -n "foundry-iq-kb-mcp-public" \
  --query "properties.configuration.ingress.fqdn" -o tsv)
echo "Public KB MCP Server URL: https://${PUBLIC_MCP_FQDN}/mcp"
```

> **Note**: The public instance needs the AI Search to have public access enabled, or you need to use a managed identity with access to the private endpoint.

---

## Step 5: Configure Private DNS

For the internal Container App to be resolvable from the VNet:

```bash
MCP_STATIC_IP=$(az containerapp env show -g $RESOURCE_GROUP -n "kb-mcp-env" \
  --query "properties.staticIp" -o tsv)
DEFAULT_DOMAIN=$(az containerapp env show -g $RESOURCE_GROUP -n "kb-mcp-env" \
  --query "properties.defaultDomain" -o tsv)

# Create private DNS zone
az network private-dns zone create -g $RESOURCE_GROUP -n $DEFAULT_DOMAIN

# Link to VNet
az network private-dns link vnet create \
  -g $RESOURCE_GROUP \
  -z $DEFAULT_DOMAIN \
  -n "kb-mcp-link" \
  -v $VNET_ID \
  --registration-enabled false

# Add wildcard A record
az network private-dns record-set a add-record \
  -g $RESOURCE_GROUP -z $DEFAULT_DOMAIN -n "*" -a $MCP_STATIC_IP
```

---

## Step 6: Run Connectivity Tests

### 6.1 KB API Connectivity

```bash
# Set environment
export AZURE_SEARCH_ENDPOINT=$SEARCH_ENDPOINT
export AZURE_SEARCH_API_KEY=$ADMIN_KEY
export AZURE_SEARCH_KB_NAME=test-kb

# Run all KB API tests
python test_kb_api_connectivity.py \
  --endpoint $SEARCH_ENDPOINT \
  --api-key $ADMIN_KEY \
  --kb-name test-kb

# Or run specific tests
python test_kb_api_connectivity.py --test connectivity
python test_kb_api_connectivity.py --test sharepoint_headers
python test_kb_api_connectivity.py --test multi_source
```

### 6.2 MCP Server Connectivity

```bash
export MCP_SERVER_URL="https://${PUBLIC_MCP_FQDN}/mcp"

python test_foundry_iq_kb_mcp.py --test connectivity
```

---

## Step 7: Run Agent Integration Tests

```bash
# Set project endpoint
export PROJECT_ENDPOINT="https://${AI_SERVICES_NAME}.services.ai.azure.com/api/projects/<project>"
export MCP_SERVER_URL="https://${PUBLIC_MCP_FQDN}/mcp"
export MCP_SERVER_PRIVATE="https://${MCP_FQDN}/mcp"

# Run all tests against public MCP server
python test_foundry_iq_kb_mcp.py --server public

# Run all tests against private MCP server (requires VPN/Bastion)
python test_foundry_iq_kb_mcp.py --server private

# With retries (for Hyena cluster routing issues)
python test_foundry_iq_kb_mcp.py --retry 3
```

---

## Step 8: Validate SharePoint Header Passthrough

The `x-ms-sharepoint-*` headers enable access to SharePoint content through Azure AI Search. These headers must be forwarded through the entire chain:

```
Client → Agent → Data Proxy → MCP Server → AI Search → SharePoint
```

Test headers:
- `x-ms-sharepoint-siteurl`: SharePoint site URL
- `x-ms-sharepoint-tenantid`: Azure AD tenant ID  
- `x-ms-sharepoint-accesstoken`: OAuth access token for SP content

```bash
# Direct MCP test with SP headers
python test_foundry_iq_kb_mcp.py --test sharepoint_headers

# Direct KB API test with SP headers
python test_kb_api_connectivity.py --test sharepoint_headers
```

---

## Troubleshooting

### MCP Server Returns Stub Data (Not Live AI Search)

The Foundry IQ KB MCP server falls back to built-in demo data when `AZURE_SEARCH_ENDPOINT` is not configured.

**Fix**: Ensure environment variables are set on the Container App:
```bash
az containerapp update \
  -g $RESOURCE_GROUP -n "foundry-iq-kb-mcp" \
  --set-env-vars \
    AZURE_SEARCH_ENDPOINT=$SEARCH_ENDPOINT \
    AZURE_SEARCH_API_KEY=$ADMIN_KEY \
    AZURE_SEARCH_KB_NAME=test-kb
```

### Agent Test: TaskCanceledException (~50%)

**Cause**: Hyena cluster has 2 scale units; Data Proxy only deployed on 1.

**Workaround**: Use `--retry 3` flag.

### Agent Test: 424 Failed Dependency

**Cause**: Data Proxy cannot resolve private Container Apps DNS.

**Workaround**: Use the public MCP server instance, or configure DNS properly (Step 5).

### KB API: 404 Not Found

**Cause**: KB index doesn't exist or name mismatch.

**Fix**: Run `create_sample_kb.py` and verify `--kb-name` matches.

### KB API: Connection Refused / DNS Error

**Cause**: AI Search has public access disabled and you're outside the VNet.

**Fix**: Either:
1. Connect via VPN/ExpressRoute/Bastion
2. Temporarily enable public access: `az search service update -g $RESOURCE_GROUP -n $AI_SEARCH_NAME --public-network-access enabled`

### Portal Shows "New Foundry Not Supported"

**Expected** when network injection is configured. Use SDK testing instead.

---

## Test Results Summary

### Test Scripts

| Script | Purpose |
|--------|---------|
| `create_sample_kb.py` | Create/populate sample KB with 12 docs (SP, index, web) |
| `test_kb_api_connectivity.py` | Direct REST tests against private AI Search |
| `test_foundry_iq_kb_mcp.py` | Full integration: MCP connectivity + Agent V2 + SP headers |

### Test Matrix

| Test | Public MCP | Private MCP | Notes |
|------|-----------|-------------|-------|
| MCP Connectivity (direct HTTP) | ✅ | ✅ (from VNet) | Session flow: init → list → retrieve |
| KB Retrieve via Agent V2 | ✅* | ✅* | *~50% fail rate (Hyena routing) |
| SharePoint Headers | ✅ | ✅ | x-ms-sharepoint-* forwarded |
| Multi-Source Retrieval | ✅ | ✅ | SharePoint + index + web results |
| KB API Connectivity | ✅ (if public) | ✅ (from VNet) | Direct REST to AI Search |

### Known Limitations

| Issue | Cause | Workaround |
|-------|-------|------------|
| ~50% TaskCanceledException | Hyena cluster 2-SU routing | `--retry 3` |
| Portal "New Foundry" blocked | Network injection | Use SDK testing |
| Private MCP DNS via Data Proxy | Container Apps DNS not resolved | Public MCP or DNS fix |
| Stub data in dev mode | No AZURE_SEARCH_ENDPOINT set | Set env vars on Container App |

---

## Cleanup

```bash
az group delete --name $RESOURCE_GROUP --yes --no-wait
```
