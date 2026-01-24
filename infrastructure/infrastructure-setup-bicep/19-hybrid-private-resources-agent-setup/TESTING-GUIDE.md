# Hybrid Private Resources - Testing Guide

This guide covers testing Azure AI Foundry agents with tools that access private resources (AI Search, MCP servers) when the AI Services account has public access enabled.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Step 1: Deploy the Template](#step-1-deploy-the-template)
3. [Step 2: Verify Private Endpoints](#step-2-verify-private-endpoints)
4. [Step 3: Create Test Data in AI Search](#step-3-create-test-data-in-ai-search)
5. [Step 4: Deploy MCP Server (Optional)](#step-4-deploy-mcp-server-optional)
6. [Step 5: Test via Portal](#step-5-test-via-portal)
7. [Step 6: Test via SDK](#step-6-test-via-sdk)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

- Azure CLI installed and authenticated
- Owner or Contributor role on the subscription
- Python 3.10+ (for SDK testing)

---

## Step 1: Deploy the Template

```bash
# Set variables
RESOURCE_GROUP="rg-hybrid-agent-test"
LOCATION="westus2"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Deploy the template
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters location=$LOCATION

# Get the deployment outputs
AI_SERVICES_NAME=$(az cognitiveservices account list -g $RESOURCE_GROUP --query "[0].name" -o tsv)
echo "AI Services: $AI_SERVICES_NAME"
```

---

## Step 2: Verify Private Endpoints

Confirm that backend resources have private endpoints but AI Services does not:

```bash
# List private endpoints
az network private-endpoint list -g $RESOURCE_GROUP -o table

# Expected: Private endpoints for:
# - AI Search (*search-private-endpoint)
# - Cosmos DB (*cosmosdb-private-endpoint)
# - Storage (*storage-private-endpoint)
# - AI Services (*-private-endpoint) - for internal Data Proxy access

# Verify AI Services is publicly accessible
AI_ENDPOINT=$(az cognitiveservices account show -g $RESOURCE_GROUP -n $AI_SERVICES_NAME --query "properties.endpoint" -o tsv)
curl -I $AI_ENDPOINT
# Should return HTTP 200 (accessible from internet)
```

---

## Step 3: Create Test Data in AI Search

Since AI Search has a private endpoint, you need to access it from within the VNet or temporarily allow public access.

### Option A: Deploy a Jump Box (Recommended)

```bash
# Get VNet and subnet info
VNET_NAME=$(az network vnet list -g $RESOURCE_GROUP --query "[0].name" -o tsv)

# Create jump box
az vm create \
  --resource-group $RESOURCE_GROUP \
  --name "jumpbox-vm" \
  --image Ubuntu2204 \
  --vnet-name $VNET_NAME \
  --subnet "pe-subnet" \
  --admin-username azureuser \
  --generate-ssh-keys \
  --assign-identity

# SSH into jump box and create index
# (See template 15 TESTING-GUIDE.md Step 4 for detailed index creation)
```

### Option B: Temporarily Enable Public Access on AI Search

```bash
AI_SEARCH_NAME=$(az search service list -g $RESOURCE_GROUP --query "[0].name" -o tsv)

# Temporarily enable public access
az search service update -g $RESOURCE_GROUP -n $AI_SEARCH_NAME \
  --public-network-access enabled

# Get admin key
ADMIN_KEY=$(az search admin-key show -g $RESOURCE_GROUP --service-name $AI_SEARCH_NAME --query "primaryKey" -o tsv)

# Create test index
curl -X POST "https://${AI_SEARCH_NAME}.search.windows.net/indexes?api-version=2023-11-01" \
  -H "Content-Type: application/json" \
  -H "api-key: ${ADMIN_KEY}" \
  -d '{
    "name": "test-index",
    "fields": [
      {"name": "id", "type": "Edm.String", "key": true},
      {"name": "content", "type": "Edm.String", "searchable": true}
    ]
  }'

# Add a test document
curl -X POST "https://${AI_SEARCH_NAME}.search.windows.net/indexes/test-index/docs/index?api-version=2023-11-01" \
  -H "Content-Type: application/json" \
  -H "api-key: ${ADMIN_KEY}" \
  -d '{
    "value": [
      {"@search.action": "upload", "id": "1", "content": "This is a test document for validating AI Search integration with Azure AI Foundry agents."}
    ]
  }'

# Disable public access again
az search service update -g $RESOURCE_GROUP -n $AI_SEARCH_NAME \
  --public-network-access disabled
```

---

## Step 4: Deploy MCP Server (Optional)

Deploy an MCP server to the private VNet:

```bash
# Get MCP subnet resource ID
MCP_SUBNET_ID=$(az network vnet subnet show -g $RESOURCE_GROUP --vnet-name $VNET_NAME -n "mcp-subnet" --query "id" -o tsv)

# Create Container Apps environment
az containerapp env create \
  --resource-group $RESOURCE_GROUP \
  --name "mcp-env" \
  --location $LOCATION \
  --infrastructure-subnet-resource-id $MCP_SUBNET_ID \
  --internal-only true

# Deploy test MCP server
az containerapp create \
  --resource-group $RESOURCE_GROUP \
  --name "mcp-test-server" \
  --environment "mcp-env" \
  --image "mcr.microsoft.com/k8se/quickstart:latest" \
  --target-port 80 \
  --ingress external \
  --min-replicas 1

# Get the FQDN and static IP
MCP_FQDN=$(az containerapp show -g $RESOURCE_GROUP -n "mcp-test-server" --query "properties.configuration.ingress.fqdn" -o tsv)
MCP_STATIC_IP=$(az containerapp env show -g $RESOURCE_GROUP -n "mcp-env" --query "properties.staticIp" -o tsv)
DEFAULT_DOMAIN=$(az containerapp env show -g $RESOURCE_GROUP -n "mcp-env" --query "properties.defaultDomain" -o tsv)

echo "MCP FQDN: $MCP_FQDN"
echo "Static IP: $MCP_STATIC_IP"

# Create private DNS zone for Container Apps
az network private-dns zone create -g $RESOURCE_GROUP -n $DEFAULT_DOMAIN

# Link to VNet
VNET_ID=$(az network vnet show -g $RESOURCE_GROUP -n $VNET_NAME --query "id" -o tsv)
az network private-dns link vnet create \
  -g $RESOURCE_GROUP \
  -z $DEFAULT_DOMAIN \
  -n "containerapp-link" \
  -v $VNET_ID \
  --registration-enabled false

# Add A records
az network private-dns record-set a add-record \
  -g $RESOURCE_GROUP \
  -z $DEFAULT_DOMAIN \
  -n "mcp-test-server" \
  -a $MCP_STATIC_IP

az network private-dns record-set a add-record \
  -g $RESOURCE_GROUP \
  -z $DEFAULT_DOMAIN \
  -n "*" \
  -a $MCP_STATIC_IP
```

---

## Step 5: Test via Portal

Since the AI Services account has public access enabled, you can test directly in the portal.

### 5.1 Access the Portal

1. Navigate to [Azure AI Foundry portal](https://ai.azure.com)
2. Sign in with your Azure credentials
3. Select your project (created by the deployment)

### 5.2 Create an Agent with AI Search Tool

1. Go to **Agents** in the left menu
2. Click **+ New agent**
3. Configure the agent:
   - **Name**: `search-test-agent`
   - **Model**: `gpt-4o-mini`
   - **Instructions**: `You are a helpful assistant. Use the search tool to find information when asked.`
4. Add a tool:
   - Click **+ Add tool**
   - Select **Azure AI Search**
   - Choose the AI Search connection created by the deployment
   - Select `test-index`
5. **Save** the agent

### 5.3 Test the Agent

1. Open the agent in the playground
2. Send a message: `Search for information about AI Foundry agents`
3. Verify the agent uses the AI Search tool and returns results from the private index

**What this proves:**
- The agent (running in the cloud) can reach the private AI Search via the Data Proxy
- The Data Proxy correctly routes through the VNet to the private endpoint

### 5.4 Create an Agent with MCP Tool (If MCP Deployed)

1. Create a new agent
2. Add an MCP tool:
   - **Server URL**: `https://<mcp-server-fqdn>`
   - **Server Label**: `test-mcp`
3. Test that the agent can discover and use tools from the MCP server

---

## Step 6: Test via SDK

For automated testing or CI/CD pipelines, use the SDK:

### 6.1 Install Dependencies

```bash
pip install azure-ai-projects azure-identity
```

### 6.2 Run Test Script

```python
#!/usr/bin/env python3
"""Test agent with AI Search tool on private endpoint."""

import os
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

# Configuration
PROJECT_ENDPOINT = os.environ.get(
    "PROJECT_ENDPOINT",
    "https://<ai-services-name>.services.ai.azure.com/api/projects/<project-name>"
)

def main():
    client = AIProjectClient(
        credential=DefaultAzureCredential(),
        endpoint=PROJECT_ENDPOINT,
    )
    
    # Create agent with AI Search tool
    agent = client.agents.create_agent(
        model="gpt-4o-mini",
        name="sdk-search-agent",
        instructions="Search for information when asked.",
        tools=[{
            "type": "azure_ai_search",
            "azure_ai_search": {
                "index_connection_id": "<connection-name>",
                "index_name": "test-index"
            }
        }]
    )
    print(f"Created agent: {agent.id}")
    
    # Create thread and test
    thread = client.agents.threads.create()
    client.agents.messages.create(
        thread_id=thread.id,
        role="user",
        content="Search for documents about AI Foundry"
    )
    
    run = client.agents.runs.create(thread_id=thread.id, agent_id=agent.id)
    print(f"Run status: {run.status}")
    
    # Cleanup
    client.agents.delete_agent(agent.id)
    print("Test complete!")

if __name__ == "__main__":
    main()
```

---

## Troubleshooting

### Portal Shows "New Foundry Not Supported"

This error occurs with **template 15** (fully private), not this template. If you see this error:
- Verify you deployed template 19 (hybrid), not template 15
- Check `publicNetworkAccess` is `Enabled` on the AI Services account

### Agent Can't Access AI Search

1. **Verify private endpoint exists**:
   ```bash
   az network private-endpoint list -g $RESOURCE_GROUP --query "[?contains(name,'search')]"
   ```

2. **Check Data Proxy configuration**:
   ```bash
   az cognitiveservices account show -g $RESOURCE_GROUP -n $AI_SERVICES_NAME \
     --query "properties.networkInjections"
   ```

3. **Verify AI Search connection in project**:
   - Go to the portal → Project → Settings → Connections
   - Confirm AI Search connection exists

### MCP Server Not Accessible

1. **Check private DNS zone**:
   ```bash
   az network private-dns record-set list -g $RESOURCE_GROUP -z $DEFAULT_DOMAIN
   ```

2. **Verify Container App is running**:
   ```bash
   az containerapp show -g $RESOURCE_GROUP -n "mcp-test-server" --query "properties.runningStatus"
   ```

---

## Cleanup

```bash
# Delete all resources
az group delete --name $RESOURCE_GROUP --yes --no-wait
```
