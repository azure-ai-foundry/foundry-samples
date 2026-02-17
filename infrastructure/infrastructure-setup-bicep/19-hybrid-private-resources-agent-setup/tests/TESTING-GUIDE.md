# Hybrid Private Resources - Testing Guide

This guide covers testing Azure AI Foundry agents with tools that access private resources (AI Search, MCP servers). By default, the Foundry (AI Services) resource has **public network access disabled**. You can optionally [switch to public access](#switching-the-foundry-resource-to-public-access) for easier development.

> **Private Foundry (default):** You need a secure connection (VPN Gateway, ExpressRoute, or Azure Bastion) to reach the Foundry resource and run SDK tests. See [Connecting to a Private Foundry Resource](#connecting-to-a-private-foundry-resource).

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Connecting to a Private Foundry Resource](#connecting-to-a-private-foundry-resource)
3. [Switching the Foundry Resource to Public Access](#switching-the-foundry-resource-to-public-access)
4. [Step 1: Deploy the Template](#step-1-deploy-the-template)
5. [Step 2: Verify Private Endpoints](#step-2-verify-private-endpoints)
6. [Step 3: Create Test Data in AI Search](#step-3-create-test-data-in-ai-search)
7. [Step 4: Deploy MCP Server](#step-4-deploy-mcp-server)
8. [Step 5: Test via SDK](#step-5-test-via-sdk)
9. [Troubleshooting](#troubleshooting)
10. [Test Results Summary](#test-results-summary)

---

## Prerequisites

- Azure CLI installed and authenticated
- Owner or Contributor role on the subscription
- Python 3.10+ (for SDK testing)

---

## Connecting to a Private Foundry Resource

When the Foundry resource has public network access **disabled** (the default), you must connect to the Azure VNet before you can reach the Foundry endpoint for SDK testing or portal access.

Azure provides three methods:

| Method | Use Case |
|--------|----------|
| **Azure VPN Gateway** | Connect from your local machine/network over an encrypted tunnel |
| **Azure ExpressRoute** | Private, dedicated connection from on-premises infrastructure |
| **Azure Bastion** | Access a jump box VM on the VNet securely through the Azure portal |

For step-by-step setup instructions, see: [Securely connect to Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link?view=foundry#securely-connect-to-foundry).

Once connected to the VNet, all SDK commands and portal interactions in this guide will work as documented.

---

## Switching the Foundry Resource to Public Access

If your security policy permits, you can enable public network access on the Foundry resource so that SDK tests and portal access work directly from the internet without VPN/ExpressRoute/Bastion.

In `modules-network-secured/ai-account-identity.bicep`, change:

```bicep
// Change from:
publicNetworkAccess: 'Disabled'
// To:
publicNetworkAccess: 'Enabled'

// Also change:
defaultAction: 'Deny'
// To:
defaultAction: 'Allow'
```

Then redeploy the template. Backend resources (AI Search, Cosmos DB, Storage) remain on private endpoints regardless of this setting.

To revert to private, set `publicNetworkAccess: 'Disabled'` and `defaultAction: 'Deny'`, then redeploy.

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

Confirm that backend resources have private endpoints:

```bash
# List private endpoints
az network private-endpoint list -g $RESOURCE_GROUP -o table

# Expected: Private endpoints for:
# - AI Search (*search-private-endpoint)
# - Cosmos DB (*cosmosdb-private-endpoint)
# - Storage (*storage-private-endpoint)
# - AI Services (*-private-endpoint)

# If public access is ENABLED, verify AI Services is publicly accessible:
AI_ENDPOINT=$(az cognitiveservices account show -g $RESOURCE_GROUP -n $AI_SERVICES_NAME --query "properties.endpoint" -o tsv)
curl -I $AI_ENDPOINT
# Should return HTTP 200 (accessible from internet)

# If public access is DISABLED (default), the curl above will fail.
# You must connect via VPN/ExpressRoute/Bastion to reach the endpoint.
# See: Connecting to a Private Foundry Resource
```

---

## Step 3: Create Test Data in AI Search

Since AI Search has a private endpoint, you need to access it from within the VNet or temporarily allow public access.

### Option A: Temporarily Enable Public Access on AI Search

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

## Step 4: Deploy MCP Server

Deploy an HTTP-based MCP server to the private VNet. 

> **Important**: Azure AI Agents require MCP servers that implement the **Streamable HTTP transport** (JSON-RPC over HTTP). Standard stdio-based MCP servers (like `mcp/hello-world`) will NOT work.

### 4.1 Create Container Apps Environment

```bash
# Create ACR if needed
ACR_NAME="mcpacr$(date +%s | tail -c 5)"
az acr create --name $ACR_NAME --resource-group $RESOURCE_GROUP --sku Basic --location $LOCATION

# Import the pre-built multi-auth MCP image
az acr import \
  --name $ACR_NAME \
  --source retrievaltestacr.azurecr.io/multi-auth-mcp/api-multi-auth-mcp-env:latest \
  --image multi-auth-mcp:latest

# Create user-assigned identity with AcrPull role
az identity create --name mcp-identity --resource-group $RESOURCE_GROUP --location $LOCATION
IDENTITY_ID=$(az identity show --name mcp-identity -g $RESOURCE_GROUP --query "id" -o tsv)
IDENTITY_PRINCIPAL=$(az identity show --name mcp-identity -g $RESOURCE_GROUP --query "principalId" -o tsv)
ACR_ID=$(az acr show --name $ACR_NAME --query "id" -o tsv)
az role assignment create --assignee $IDENTITY_PRINCIPAL --role AcrPull --scope $ACR_ID

# Wait for role assignment to propagate
sleep 30
```

### 4.2 Create Container Apps Environment

```bash
VNET_NAME=$(az network vnet list -g $RESOURCE_GROUP --query "[0].name" -o tsv)
MCP_SUBNET_ID=$(az network vnet subnet show -g $RESOURCE_GROUP --vnet-name $VNET_NAME -n "mcp-subnet" --query "id" -o tsv)

# Create Container Apps environment (internal only)
az containerapp env create \
  --resource-group $RESOURCE_GROUP \
  --name "mcp-env" \
  --location $LOCATION \
  --infrastructure-subnet-resource-id $MCP_SUBNET_ID \
  --internal-only true
```

### 4.2 Deploy HTTP-based MCP Server

An HTTP-based MCP server is provided in `mcp-http-server/`. Deploy it:

```bash
# Build and deploy (requires ACR with managed identity access)
cd mcp-http-server

# Create ACR and build
ACR_NAME="mcpacr$(date +%s | tail -c 5)"
az acr create --name $ACR_NAME --resource-group $RESOURCE_GROUP --sku Basic --location $LOCATION
az acr build --registry $ACR_NAME --image mcp-hello-http:v1 --file Dockerfile .

# Create user-assigned identity with AcrPull role
az identity create --name mcp-identity --resource-group $RESOURCE_GROUP --location $LOCATION
IDENTITY_ID=$(az identity show --name mcp-identity -g $RESOURCE_GROUP --query "id" -o tsv)
IDENTITY_PRINCIPAL=$(az identity show --name mcp-identity -g $RESOURCE_GROUP --query "principalId" -o tsv)
ACR_ID=$(az acr show --name $ACR_NAME --query "id" -o tsv)
az role assignment create --assignee $IDENTITY_PRINCIPAL --role AcrPull --scope $ACR_ID

# Deploy container app
az containerapp create \
  --resource-group $RESOURCE_GROUP \
  --name "mcp-http-server" \
  --environment "mcp-env" \
  --image "${ACR_NAME}.azurecr.io/mcp-hello-http:v1" \
  --target-port 80 \
  --ingress internal \
  --min-replicas 1 \
  --user-assigned $IDENTITY_ID \
  --registry-server "${ACR_NAME}.azurecr.io" \
  --registry-identity $IDENTITY_ID
```

### 4.3 Configure Private DNS

```bash
# Get environment info
MCP_STATIC_IP=$(az containerapp env show -g $RESOURCE_GROUP -n "mcp-env" --query "properties.staticIp" -o tsv)
DEFAULT_DOMAIN=$(az containerapp env show -g $RESOURCE_GROUP -n "mcp-env" --query "properties.defaultDomain" -o tsv)
MCP_FQDN=$(az containerapp show -g $RESOURCE_GROUP -n "mcp-http-server" --query "properties.configuration.ingress.fqdn" -o tsv)

# Create private DNS zone
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
az network private-dns record-set a add-record -g $RESOURCE_GROUP -z $DEFAULT_DOMAIN -n "mcp-http-server" -a $MCP_STATIC_IP
az network private-dns record-set a add-record -g $RESOURCE_GROUP -z $DEFAULT_DOMAIN -n "*" -a $MCP_STATIC_IP
```

### 4.4 Test MCP with REST API

```python
import requests
from azure.identity import DefaultAzureCredential
import time

credential = DefaultAzureCredential()
token = credential.get_token("https://ai.azure.com/.default")

endpoint = "https://<ai-services>.services.ai.azure.com/api/projects/<project>"
api_version = "2025-05-15-preview"
mcp_url = "https://mcp-http-server.<default-domain>"

headers = {"Authorization": f"Bearer {token.token}", "Content-Type": "application/json"}

# Create agent with MCP tool
agent_payload = {
    "model": "gpt-4o-mini",
    "name": "mcp-test-agent",
    "instructions": "Use the hello tool to greet users.",
    "tools": [{"type": "mcp", "server_label": "helloworld", "server_url": mcp_url}]
}
resp = requests.post(f"{endpoint}/assistants?api-version={api_version}", headers=headers, json=agent_payload)
agent = resp.json()
print(f"Agent: {agent['id']}")
```

### 4.5 (Optional) Deploy Public MCP Server for Testing

## Step 5: Test via Portal

> **Note**: Portal testing may be blocked even with public access enabled if your deployment uses network injection (`networkInjections` property). In this case, use SDK testing (Step 6) instead.

### 5.1 Check if Portal Works

1. Navigate to [Azure AI Foundry portal](https://ai.azure.com)
2. Sign in with your Azure credentials
3. Toggle **"New Foundry"** ON (top right)
4. Select your project

If you see this error:
> "Your current setup uses a project, resource, region, custom domain, or disabled public network access that isn't supported in the new Foundry experience yet."

This is expected if network injection is configured. Use SDK testing instead.

### 5.2 Create an Agent with AI Search Tool (if portal works)

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
pip install azure-ai-projects azure-ai-agents azure-identity
```

---

Use the included `test_agents_v2.py` script or the following code:

```python
#!/usr/bin/env python3
"""Test agent with AI Search tool on private endpoint."""

import os
import time
from azure.ai.projects import AIProjectClient
from azure.ai.agents.models import AzureAISearchTool
from azure.identity import DefaultAzureCredential

# Configuration - use project-scoped endpoint
PROJECT_ENDPOINT = os.environ.get(
    "PROJECT_ENDPOINT",
    "https://<ai-services-name>.services.ai.azure.com/api/projects/<project-name>"
)
AI_SEARCH_CONNECTION = os.environ.get("AI_SEARCH_CONNECTION", "<connection-name>")
AI_SEARCH_INDEX = os.environ.get("AI_SEARCH_INDEX", "test-index")

def main():
    client = AIProjectClient(
        credential=DefaultAzureCredential(),
        endpoint=PROJECT_ENDPOINT,
    )
    print(f"Connected to: {PROJECT_ENDPOINT}")
    
    # Create AI Search tool using the SDK class (NOT dict format)
    search_tool = AzureAISearchTool(
        index_connection_id=AI_SEARCH_CONNECTION,
        index_name=AI_SEARCH_INDEX
    )
    
    # Create agent with AI Search tool
    agent = client.agents.create_agent(
        model="gpt-4o-mini",
        name="sdk-search-agent",
        instructions="Search for information when asked.",
        tools=search_tool.definitions,
        tool_resources=search_tool.resources
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
    print(f"Started run: {run.id}")
    
    # Wait for completion
    while run.status in ["queued", "in_progress"]:
        time.sleep(2)
        run = client.agents.runs.get(thread_id=thread.id, run_id=run.id)
        print(f"Status: {run.status}")
    
    if run.status == "completed":
        messages = client.agents.messages.list(thread_id=thread.id)
        for msg in messages:
            if msg.role == "assistant":
                for content in msg.content:
                    if hasattr(content, 'text'):
                        print(f"Response: {content.text.value}")
                break
        print("✓ Test passed!")
    else:
        print(f"✗ Run failed: {run.status}")
    
    # Cleanup
    client.agents.delete_agent(agent.id)
    print("Agent cleaned up")

```bash
pip install azure-ai-projects azure-identity openai
```

### 6.3 Find Your Connection Name

```bash
# List connections in your project
az rest --method GET \
  --url "https://management.azure.com/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<ai-services>/projects/<project>/connections?api-version=2025-06-01" \
  --query "value[?properties.category=='CognitiveSearch'].name" -o tsv
```

---

## Troubleshooting

### Portal Shows "New Foundry Not Supported"

This error can occur even with public access enabled if **network injection** is configured:

```bash
# Check for network injection
az cognitiveservices account show -g $RESOURCE_GROUP -n $AI_SERVICES_NAME \
  --query "properties.networkInjections"
```

If you see `networkInjections` with a subnet configured, the portal's "New Foundry" experience won't work. **Use SDK testing instead** - it works perfectly with network injection.

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

### MCP Tool Fails with TaskCanceledException

This is a **known issue** with the Hyena cluster infrastructure:
- The Data Proxy is deployed on only **one of two scale units**
- The load balancer routes requests in **round-robin** fashion
- ~50% of requests hit the wrong scale unit and get `TaskCanceledException`

**Workaround**: Use `--retry` flag when running tests:
```bash
python test_mcp_tools_agents_v2.py --test public --retry 3
```

### MCP Tool Fails with 400 Bad Request

Check the error message for details:
- **404 Not Found**: Verify the MCP server URL includes the correct path (`/noauth/mcp`)
- **DNS resolution**: Ensure private DNS zone is configured correctly for Container Apps

### MCP Server Not Responding

1. **Check container app health**:
   ```bash
   az containerapp show -g $RESOURCE_GROUP -n "mcp-http-server" --query "properties.runningStatus"
   ```

2. **Check container logs**:
   ```bash
   az containerapp logs show -g $RESOURCE_GROUP -n "mcp-http-server" --tail 50
   ```

3. **Verify ingress port is 8080** (not 80):
   ```bash
   az containerapp ingress show -g $RESOURCE_GROUP -n "mcp-http-server" --query "targetPort"
   ```

### Portal Shows "New Foundry Not Supported"

This is expected when network injection is configured. Use SDK testing instead - it works perfectly with network injection.

---

## Test Results Summary

### Test Scripts

| Script | Purpose |
|--------|---------|
| `test_agents_v2.py` | Full test suite: OpenAI API, basic agent, AI Search, MCP |
| `test_mcp_tools_agents_v2.py` | Focused MCP testing with retry support |

### Validated ✅

| Test | Status | Notes |
|------|--------|-------|
| OpenAI Responses API (direct) | ✅ Pass | Works from anywhere |
| Basic Agent (no tools) | ✅ Pass | Works from anywhere |
| AI Search Tool | ✅ Pass | Data Proxy routes to private endpoint |
| MCP Connectivity (direct HTTP) | ✅ Pass | Server responds correctly |
| MCP Tool via Agent (public server) | ✅ Pass* | *~50% fail rate due to Hyena routing |

### Known Limitations ⚠️

| Issue | Cause | Workaround |
|-------|-------|------------|
| ~50% TaskCanceledException | Hyena cluster has 2 scale units, Data Proxy only on 1 | Use `--retry` flag |
| Portal "New Foundry" blocked | Network injection not supported in portal | Use SDK testing |
| Private MCP via Data Proxy | DNS resolution issues for Container Apps | Use public MCP server |

### Architecture Notes

1. **AI Search Tool works** because it uses Azure Private Endpoints with built-in DNS integration (`privatelink.search.windows.net`).

2. **MCP uses Streamable HTTP transport** - The multi-auth MCP server implements proper session management with `mcp-session-id` headers required by Azure's MCP client.

3. **Container Apps require port 8080** - The multi-auth MCP image runs on port 8080, not 80.

4. **Use `/noauth/mcp` endpoint** for testing without authentication. Production deployments should use `/mcp` with proper auth configuration.

---

## Cleanup

```bash
# Delete all resources
az group delete --name $RESOURCE_GROUP --yes --no-wait
```
