# Azure Function Behind VNet — Calculator with Private Storage

A minimal Azure Function that demonstrates the **real value** of VNet Integration:
the function performs arithmetic AND stores results in a **private Azure Blob Storage
account** that has no public endpoint. Without VNet Integration, the storage write fails.

## Key Concepts

> **"Azure Functions behind a VNet"** means the Function App uses **VNet Integration**
> for outbound traffic, letting it reach private resources (databases, storage, APIs)
> that only the VNet can access. The function itself remains publicly accessible
> (`publicNetworkAccess: Enabled`) — the "private" part is what the function can *reach*,
> not who can *call* it.

### Why This Matters

```
WITHOUT VNet Integration:          WITH VNet Integration:
  calculate → ✅ works               calculate → ✅ works
  store result → ❌ fails             store result → ✅ succeeds
  (can't reach private storage)      (VNet routes to private storage)
```

The `storage.stored` field in the API response is the **proof point**: if it's `true`,
VNet Integration is working. If it's `false`, the function can compute but can't reach
private resources.

### `publicNetworkAccess` Must Be `Enabled`

When a Function App is used as an **OpenAPI tool** with the Foundry DataProxy, setting
`publicNetworkAccess: Disabled` causes `403 Ip Forbidden`. The DataProxy resolves DNS
at the Foundry infrastructure level, not through your VNet's private DNS zones.

> Use [App Service access restrictions](https://learn.microsoft.com/azure/app-service/app-service-ip-restrictions)
> if you need to limit inbound traffic to specific IP ranges.

See the [TESTING-GUIDE.md](../tests/TESTING-GUIDE.md#azure-functions-behind-a-vnet) for
the full conceptual explanation.

## Architecture

```
Agent (Foundry)
   │
   │  OpenApiTool call → DataProxy
   ▼
Azure Function App (publicNetworkAccess: Enabled)
   │  POST /api/calculate
   │
   ├─ Compute: 12 × 8 = 96  ← works without VNet
   │
   └─ Store result in Blob  ← requires VNet Integration
      │
      │  outbound via VNet Integration
      ▼
   Private Storage Account (publicNetworkAccess: Disabled)
      └─ calculation-history/20260331T150000_multiply_12_8.json
```

## Endpoints

| Endpoint | Method | Description | VNet Required? |
|----------|--------|-------------|:--------------:|
| `/api/calculate` | POST | Compute + store result in private blob | Store: ✅ Yes |
| `/api/history` | GET | Read calculation history from private blob | ✅ Yes |
| `/api/healthz` | GET | Health check + private storage connectivity | Report: ✅ Yes |

## Files

| File | Description |
|------|-------------|
| `function_app.py` | Azure Function: calculate + store in private blob, history, healthz |
| `host.json` | Functions host configuration |
| `requirements.txt` | Python dependencies (`azure-functions`, `azure-storage-blob`) |
| `local.settings.json` | Local development settings |
| `calculator_openapi.json` | OpenAPI 3.1 spec with storage status in response |
| `deploy-function.bicep` | Bicep template for VNet-secured deployment |

## Quick Start — Local Development

```bash
# Install Azure Functions Core Tools (if not installed)
npm install -g azure-functions-core-tools@4

# Start the function locally
cd azure-function-server
pip install -r requirements.txt
func start

# Test it
curl -X POST http://localhost:7071/api/calculate \
  -H "Content-Type: application/json" \
  -d '{"operation": "multiply", "a": 6, "b": 7}'
# → {"operation": "multiply", "a": 6.0, "b": 7.0, "result": 42.0}
```

## Deploy Behind VNet

### Option A: Bicep Template

```bash
RESOURCE_GROUP="rg-private-network-test"
VNET_NAME="agent-vnet-test"
PE_SUBNET="private-endpoints"  # subnet where PEs are created

az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file deploy-function.bicep \
  --parameters \
    vnetName=$VNET_NAME \
    privateEndpointSubnetName=$PE_SUBNET \
    location=westus2

# Deploy the function code
FUNC_APP_NAME=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name deploy-function \
  --query properties.outputs.functionAppName.value -o tsv)

func azure functionapp publish $FUNC_APP_NAME
```

### Option B: Manual Setup (Validated)

This is the sequence we validated end-to-end:

1. Create a **delegated subnet** for VNet Integration (`Microsoft.Web/serverFarms`)
2. Create a **Storage Account** — leave public access enabled during setup
3. Create **Storage Private Endpoints** (Blob + Queue + File) with DNS zone groups
4. Create an **Elastic Premium (EP1)** App Service Plan (`--is-linux`)
5. Create the **Function App** with `--vnet` and `--subnet` for VNet Integration
6. Set app settings: `WEBSITE_CONTENTOVERVNET=1`, `WEBSITE_VNET_ROUTE_ALL=1`
7. **Deploy the function code** with `func azure functionapp publish`
8. Verify the function works (`curl /api/healthz`)
9. Optionally lock down **storage** (set default action to Deny) — but monitor that the runtime stays healthy

> ⚠️ **Do NOT set `publicNetworkAccess: Disabled`** on the Function App if it will be
> used as an OpenAPI tool with the DataProxy. The DataProxy resolves DNS at the Foundry
> level and its traffic will be rejected with `403 Ip Forbidden`. See
> [Key Concepts](#important-publicnetworkaccess-and-the-dataproxy) above.

> ⚠️ **Storage chicken-and-egg**: The Function App creation requires access to the
> storage account to create a file share. If storage has `defaultAction: Deny`, creation
> fails with `403 Forbidden`. Temporarily allow public access, create the Function App
> and deploy code, then restrict storage access. After locking storage, **restart the
> Function App** — otherwise it may show `503 Application Error`.

## Test as OpenAPI Tool

Once deployed, run the dedicated test script:

```bash
cd ../tests

# Set the Function App URL (same URL for both — VNet Integration is outbound)
export FUNCTION_APP_PUBLIC="https://<func-app-name>.azurewebsites.net"
export FUNCTION_APP_PRIVATE="https://<func-app-name>.azurewebsites.net"

# Run all tests (connectivity + agent)
python test_azure_function_agents_v2.py --test all --retry 3

# Or test only the agent flow
python test_azure_function_agents_v2.py --test private --retry 3
```

The test loads the OpenAPI spec from `azure-function-server/calculator_openapi.json`
(note the `/api/calculate` path — Azure Functions use `/api/` prefix by default).

## Differences from the OpenAPI Server (FastAPI)

| Aspect | OpenAPI Server (FastAPI) | Azure Function |
|--------|------------------------|----------------|
| **Runtime** | uvicorn + FastAPI | Azure Functions runtime |
| **Route prefix** | `/calculate` | `/api/calculate` |
| **Hosting** | Container App | Function App (Elastic Premium) |
| **"Private" mechanism** | Internal FQDN (no public endpoint) | VNet Integration (outbound to private resources) |
| **Inbound access** | Private only (internal LB) | Public endpoint (`publicNetworkAccess: Enabled` required) |
| **DNS** | `*.azurecontainerapps.io` (private) | `*.azurewebsites.net` (public hostname) |
| **VNet proof point** | DNS resolution fails without VNet | `storage.stored: false` without VNet Integration |
| **Storage dependency** | None | Blob + Queue + File storage (with PEs when storage is locked) |
| **Storage dependency** | None | Blob + Queue storage (with PEs in VNet mode) |
| **OpenAPI spec** | Auto-generated by FastAPI | Static `calculator_openapi.json` |

## Troubleshooting

### `403 Ip Forbidden` when used as OpenAPI tool

The DataProxy resolves DNS at the Foundry infrastructure level, not through your VNet's
private DNS zones. With `publicNetworkAccess: Disabled`, the Function App rejects the
DataProxy's traffic. **Solution**: Keep `publicNetworkAccess: Enabled`. Use
[access restrictions](https://learn.microsoft.com/azure/app-service/app-service-ip-restrictions)
if you need to limit inbound access to specific IP ranges.

### `503 Application Error` / Function App won't start after enabling VNet

Two common causes:

1. **Storage access lost**: If you set the storage `defaultAction: Deny` after creating
   the Function App, the runtime loses access to its backing storage. **Restart the
   Function App** after storage changes. Ensure Storage Private Endpoints exist for
   **both Blob and Queue**.

2. **Storage PEs missing**: The Functions runtime requires access to Blob, Queue,
   **and File** storage. Without Private Endpoints for all three, the runtime cannot
   start when storage network access is restricted. The File PE is for the content
   share (`WEBSITE_CONTENTSHARE`) and is often forgotten.

### DNS resolution fails for the Function App

If using Private Endpoints (for non-DataProxy callers on the VNet), verify the Private
DNS Zone `privatelink.azurewebsites.net` is:
1. Created in the same subscription
2. Linked to the VNet
3. Has an A record for the Function App hostname

### 404 on /calculate (without /api/ prefix)

Azure Functions use `/api/` as the default route prefix. Either:
- Use `/api/calculate` in your OpenAPI spec (as in `calculator_openapi.json`)
- Or set `"routePrefix": ""` in `host.json` to remove the prefix

### Storage file share creation fails with 403 during `az functionapp create`

The Function App creation needs to create a file share in the storage account. If
storage has `publicNetworkAccess: Disabled` or `defaultAction: Deny`, this fails.
**Solution**: Temporarily set `defaultAction: Allow`, create the Function App,
deploy code, then restrict storage access again.
