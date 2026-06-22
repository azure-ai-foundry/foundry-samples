# Azure Function Behind VNet — Calculator with Private Storage (Terraform)

A minimal Azure Function that demonstrates **VNet Integration**: the function performs arithmetic AND stores results in an **Azure Blob Storage account** with private endpoints. The storage account is initially deployed with public access enabled (required for the Functions runtime file share), but traffic routes through private endpoints via VNet Integration.

This is the Terraform equivalent of the [Bicep `deploy-function.bicep`](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server/deploy-function.bicep).

## Key Concepts

> **"Azure Functions behind a VNet"** means the Function App uses **VNet Integration**
> for outbound traffic, letting it reach private resources (databases, storage, APIs)
> that only the VNet can access. The function itself remains publicly accessible
> (`publicNetworkAccess: Enabled`) — the "private" part is what the function can *reach*,
> not who can *call* it.

### `publicNetworkAccess` Must Be `Enabled`

When a Function App is used as an **OpenAPI tool** with the Foundry DataProxy, setting `publicNetworkAccess: Disabled` causes `403 Ip Forbidden`. The DataProxy resolves DNS at the Foundry infrastructure level, not through your VNet's private DNS zones.

> Use [App Service access restrictions](https://learn.microsoft.com/azure/app-service/app-service-ip-restrictions) if you need to limit inbound traffic to specific IP ranges.

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
   Storage Account (publicNetworkAccess: Enabled, with Private Endpoints)
      └─ calculation-history/20260331T150000_multiply_12_8.json
```

> **Note:** Storage is deployed with public access enabled (required for the Functions
> runtime file share during provisioning). You can restrict to `default_action = "Deny"`
> after deployment — restart the Function App afterward to avoid 503 errors.

## What Gets Deployed

| Resource | Type | Purpose |
|----------|------|---------|
| Integration Subnet | `azurerm_subnet` | Delegated to `Microsoft.Web/serverFarms` for outbound VNet Integration |
| Storage Account | `azurerm_storage_account` | Functions runtime backing store (Blob + Queue + File) |
| Storage File Share | `azurerm_storage_share` | Content share for `WEBSITE_CONTENTSHARE` (required with `WEBSITE_CONTENTOVERVNET`) |
| Storage PEs (3) | `azurerm_private_endpoint` | Blob, Queue, File private endpoints |
| Storage DNS Zones (2) | `azurerm_private_dns_zone` | `privatelink.queue/file.core.windows.net` (blob zone reused from base deployment) |
| App Service Plan | `azurerm_service_plan` | Elastic Premium EP1 (Linux, required for VNet features) |
| Function App | `azurerm_linux_function_app` | Python 3.11, VNet Integration enabled |
| Function PE | `azurerm_private_endpoint` | Inbound private access from VNet callers |
| Function DNS Zone | `azurerm_private_dns_zone` | `privatelink.azurewebsites.net` |

## Prerequisites

- Base TF 19 infrastructure deployed (VNet, subnets, DNS zones)
- `resource_group_name` and `vnet_name` from the base deployment outputs
- An available address prefix for the integration subnet (default: `192.168.5.0/24`)

## Deploy

### 1. Configure variables

```bash
cd infrastructure/infrastructure-setup-terraform/19-private-network-agent-setup-with-tools/tools/azure-function
cp example.tfvars terraform.tfvars
```

Edit `terraform.tfvars` with your base deployment's resource group and VNet names.

### 2. Deploy infrastructure

```bash
terraform init
terraform plan -var-file="terraform.tfvars" -out=tfplan
terraform apply tfplan
```

### 3. Deploy function code

The function app code is shared with the Bicep template. Copy from the Bicep 19 directory and deploy:

```bash
# Copy function code from the shared Bicep 19 location
FUNC_CODE_DIR="../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server"

# Get the Function App name from Terraform output
FUNC_APP_NAME=$(terraform output -raw function_app_name)

# Deploy the function code
cd $FUNC_CODE_DIR
func azure functionapp publish $FUNC_APP_NAME
```

### 4. Verify

```bash
# Health check — should show storage reachable via VNet
curl "https://$(terraform output -raw function_app_hostname)/api/healthz"

# Test calculation with private storage write
curl -X POST "https://$(terraform output -raw function_app_hostname)/api/calculate" \
  -H "Content-Type: application/json" \
  -d '{"operation": "multiply", "a": 6, "b": 7}'
```

The `storage.stored: true` field in the response proves VNet Integration is working.

## Function App Code

The function app code (Python) is shared across Bicep and Terraform deployments:

| File | Location | Description |
|------|----------|-------------|
| `function_app.py` | [Bicep 19 azure-function-server/](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server/function_app.py) | Calculate + store in private blob, history, healthz |
| `host.json` | [Bicep 19 azure-function-server/](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server/host.json) | Functions host configuration |
| `requirements.txt` | [Bicep 19 azure-function-server/](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server/requirements.txt) | `azure-functions`, `azure-storage-blob` |
| `calculator_openapi.json` | [Bicep 19 azure-function-server/](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server/calculator_openapi.json) | OpenAPI 3.1 spec for the calculator API |

## Troubleshooting

### `403 Ip Forbidden` when used as OpenAPI tool

Keep `publicNetworkAccess: Enabled` on the Function App. The DataProxy resolves DNS at the Foundry level, not through VNet private DNS zones.

### `503 Application Error` after restricting storage

If you lock down the storage account (`default_action = "Deny"`) after deployment, **restart the Function App**. The runtime needs to re-establish connections through VNet Integration.

### Storage file share creation fails with 403

The Function App creation needs to create a file share. If storage has `default_action = "Deny"`, this fails. Deploy with `"Allow"` first, then restrict after the Function App is created.

## Reference

- [Bicep 19 Azure Function server](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/azure-function-server/) — Shared function app code and Bicep template
- [Bicep 19 Testing Guide](../../../../infrastructure-setup-bicep/19-private-network-agent-tools/tests/TESTING-GUIDE.md) — End-to-end testing for tools behind VNet
- [Azure Functions VNet Integration](https://learn.microsoft.com/azure/azure-functions/functions-networking-options#virtual-network-integration)
- [App Service access restrictions](https://learn.microsoft.com/azure/app-service/app-service-ip-restrictions)
