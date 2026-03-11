# Hybrid Private Resources Agent Setup

This template deploys an Azure AI Foundry account with backend resources (AI Search, Cosmos DB, Storage) on **private endpoints**. By default, the Foundry resource itself also has **public network access disabled**, but this can be switched to public access if needed (see [Switching Between Private and Public Access](#switching-between-private-and-public-access)).

## Architecture (Default — Private Foundry)

```
┌─────────────────────────────────────────────────────────────────────┐
│  Secure Access (VPN Gateway / ExpressRoute / Azure Bastion)         │
└──────────────────────────────────┬──────────────────────────────────┘
                                   │
                    ┌──────────────▼──────────────┐
                    │      AI Services Account     │
                    │   (publicNetworkAccess:      │
                    │        DISABLED)             │  ◄── Private by default
                    │                              │
                    │  ┌────────────────────────┐  │
                    │  │   Data Proxy / Agent   │  │
                    │  │      ToolServer        │  │
                    │  └───────────┬────────────┘  │
                    └──────────────┼──────────────┘
                                   │ networkInjections
                    ┌──────────────▼──────────────┐
                    │     Private VNet             │
                    │                              │
                    │  ┌─────────┐ ┌─────────┐    │
                    │  │AI Search│ │Cosmos DB│    │  ◄── Private endpoints
                    │  └─────────┘ └─────────┘    │      (no public access)
                    │                              │
                    │  ┌─────────┐ ┌─────────┐    │
                    │  │ Storage │ │   MCP   │    │
                    │  └─────────┘ │ Servers │    │
                    │              └─────────┘    │
                    └─────────────────────────────┘
```

## Key Features

| Feature | This Template (19) — Private (default) | This Template (19) — Public | Fully Private (15) |
|---------|----------------------------------------|-----------------------------|-----------------------|
| AI Services public access | ❌ Disabled | ✅ Enabled | ❌ Disabled |
| Portal access | Via VPN/ExpressRoute/Bastion | ✅ Works directly | Via VPN/ExpressRoute/Bastion |
| Backend resources | 🔒 Private | 🔒 Private | 🔒 Private |
| Data Proxy | ✅ Configured | ✅ Configured | ✅ Configured |
| Secure connection required | ✅ Yes | ❌ No | ✅ Yes |

## Switching Between Private and Public Access

The Foundry resource has **public network access disabled by default**. You can switch between the two modes by modifying the Bicep template.

### To enable public access

In [modules-network-secured/ai-account-identity.bicep](modules-network-secured/ai-account-identity.bicep), change:

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

This makes the Foundry resource accessible from the internet (e.g., for portal-based development without VPN).

### To disable public access (default)

Revert the changes above, setting `publicNetworkAccess: 'Disabled'` and `defaultAction: 'Deny'`.

## Connecting to a Private Foundry Resource

When public network access is disabled (the default), you need a secure connection to reach the Foundry resource. Azure provides three methods:

1. **Azure VPN Gateway** — Connect from your local network to the Azure VNet over an encrypted tunnel.
2. **Azure ExpressRoute** — Use a private, dedicated connection from your on-premises infrastructure to Azure.
3. **Azure Bastion** — Use a jump box VM on the VNet, accessed securely through the Azure portal.

For detailed setup instructions, see: [Securely connect to Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/configure-private-link?view=foundry#securely-connect-to-foundry).

## When to Use This Template

Use this template when you want:
- **Private backend resources** — Keep AI Search, Cosmos DB, and Storage behind private endpoints
- **MCP server integration** — Deploy MCP servers on the VNet that agents can access via Data Proxy
- **Private Foundry (default)** — Full network isolation with secure access via VPN/ExpressRoute/Bastion
- **Optional public Foundry access** — Switch to public for portal-based development if allowed by your security policy

## When NOT to Use This Template

Use [template 15](../15-private-network-standard-agent-setup/) instead when you need:
- **Fully managed private networking** — Including managed VNet with Microsoft-managed private endpoints
- **Compliance requirements** — Regulations that require a different private networking topology

## Deployment

### Prerequisites

1. Azure CLI installed and authenticated
2. Owner or Contributor role on the subscription
3. Sufficient quota for model deployment (gpt-4o-mini)

### Deploy

```bash
# Create resource group
az group create --name "rg-hybrid-agent-test" --location "westus2"

# Deploy the template
az deployment group create \
  --resource-group "rg-hybrid-agent-test" \
  --template-file main.bicep \
  --parameters location="westus2"
```

### Verify Deployment

```bash
# Check deployment status
az deployment group show \
  --resource-group "rg-hybrid-agent-test" \
  --name "main" \
  --query "properties.provisioningState"

# List private endpoints (should see AI Search, Storage, Cosmos DB)
az network private-endpoint list \
  --resource-group "rg-hybrid-agent-test" \
  --output table
```

## Testing Agents with Private Resources

### Option 1: Portal Testing

If the Foundry resource has **public network access enabled**, you can test directly in the portal:

1. Navigate to [Azure AI Foundry portal](https://ai.azure.com)
2. Select your project
3. Create an agent with AI Search tool
4. Test that the agent can query the private AI Search index

If the Foundry resource has **public network access disabled** (default), you need to connect via VPN Gateway, ExpressRoute, or Azure Bastion before accessing the portal. See [Connecting to a Private Foundry Resource](#connecting-to-a-private-foundry-resource).

### Option 2: SDK Testing

See [tests/TESTING-GUIDE.md](tests/TESTING-GUIDE.md) for detailed SDK testing instructions.

## MCP Server Deployment

To deploy MCP servers on the private VNet:

```bash
# Create Container Apps environment on mcp-subnet
az containerapp env create \
  --resource-group "rg-hybrid-agent-test" \
  --name "mcp-env" \
  --location "westus2" \
  --infrastructure-subnet-resource-id "<mcp-subnet-resource-id>" \
  --internal-only true

# Deploy MCP server
az containerapp create \
  --resource-group "rg-hybrid-agent-test" \
  --name "my-mcp-server" \
  --environment "mcp-env" \
  --image "<your-mcp-image>" \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1
```

Then configure private DNS zone for Container Apps (see TESTING-GUIDE.md Step 6.3).

## Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `location` | Azure region | `eastus2` |
| `aiServices` | Base name for AI Services | `aiservices` |
| `modelName` | Model to deploy | `gpt-4o-mini` |
| `modelCapacity` | TPM capacity | `30` |
| `vnetName` | VNet name | `agent-vnet-test` |
| `agentSubnetName` | Subnet for AI Foundry (reserved) | `agent-subnet` |
| `peSubnetName` | Subnet for private endpoints | `pe-subnet` |
| `mcpSubnetName` | Subnet for MCP servers | `mcp-subnet` |

## Cleanup

```bash
# Delete all resources
az group delete --name "rg-hybrid-agent-test" --yes --no-wait
```

## Related Templates

- [15-private-network-standard-agent-setup](../15-private-network-standard-agent-setup/) - Fully private setup (no public access)
- [40-basic-agent-setup](../40-basic-agent-setup/) - Basic agent setup without private networking
- [41-standard-agent-setup](../41-standard-agent-setup/) - Standard agent setup without private networking
