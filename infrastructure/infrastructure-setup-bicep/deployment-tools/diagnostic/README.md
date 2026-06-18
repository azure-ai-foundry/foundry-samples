# Post-Deployment Diagnostic for Foundry Private Network

Validates that all resources, networking, RBAC, and capability hosts are healthy after deploying a Foundry private network template (Bicep or Terraform).

## What It Checks

Checks are ordered **outside-in**, following the network path an agent request takes from the Data Proxy through the VNet to backend resources:

| # | Area | What's Verified |
|---|------|----------------|
| 1 | Discovery | Auto-discovers AIServices accounts in the resource group |
| 2 | Network Injection | Data Proxy config and subnet reference valid — is the platform infra alive? |
| 3 | VNet & Subnets | Delegations correct, ServiceAssociationLinks present on agent subnet |
| 4 | NSG Rules | Custom NSG rules don't block required traffic (443/445 outbound, VNet inbound on PE/MCP subnets) |
| 5 | DNS Zones | Private DNS zones exist with VNet links |
| 6 | Custom DNS | Detects whether VNet uses custom DNS servers or Azure default DNS; if custom, reports server IPs and required forwarder target (`168.63.129.16`) |
| 7 | Private Endpoints | PE connections, shared PEs (resourceAccessRules), network rules, bypass, and shared key per resource |
| 8 | Projects & MI | Project provisioned with system-assigned managed identity |
| 9 | Capability Host | Project (and optionally account) caphost in `Succeeded` state with connections wired |
| 10 | Connections | Cosmos DB, Storage, and AI Search project connections exist with AAD auth |
| 11 | RBAC | All 5 ARM roles + Cosmos SQL data-plane role assigned to project MI |
| 12 | Provisioning | All resources in `Succeeded` state |
| 13 | Public Access + ACLs | `publicNetworkAccess: Disabled` on all resources, AI Services ACL `Deny` + `AzureServices` bypass |
| 14 | Model Deployment | At least one model deployed and healthy |
| 15 | Azure Policy | Non-compliant policy evaluations, Deny-effect policies that block deployment |

## Usage

### With config file

```powershell
cp diagnostic.config.sample diagnostic.config
# Edit diagnostic.config with your values
.\diagnostic-check.ps1 -ConfigFile .\diagnostic.config
```

### With parameters

```powershell
.\diagnostic-check.ps1 -SubscriptionId "your-sub-id" -ResourceGroup "your-rg"
```

### With specific account

```powershell
.\diagnostic-check.ps1 -SubscriptionId "your-sub-id" -ResourceGroup "your-rg" -AccountName "aifoundryabcd"
```

## Prerequisites

- PowerShell 7+ (`pwsh`) — Windows PowerShell 5.1 is not supported
- Azure CLI installed and logged in (`az login`)
- Active subscription set to the target subscription
- Reader access on the resource group (Contributor for full RBAC checks)

## Reading the Output

- **[PASS]** — check passed
- **[FAIL]** — something is wrong that will likely break agent functionality
- **[WARN]** — unexpected configuration that may or may not cause issues
- **[INFO]** — informational (BYO resources in other RGs, etc.)

## Common Failure Patterns

| Symptom | Likely Cause | Check # |
|---------|-------------|---------|
| Agent calls timeout | Network injection subnet missing or deleted | 2 |
| Caphost failed | SAL conflict on subnet from prior deployment — use a new VNet | 3, 9 |
| NSG blocks agent traffic | Custom NSG deny-all outbound without AzureCloud allow | 4 |
| MCP tools unreachable | NSG on MCP subnet blocks inbound from VNet | 4 |
| DNS resolution fails | DNS zone not linked to VNet, or custom DNS without forwarders | 5, 6 |
| Private endpoint returns public IP | Custom DNS servers missing conditional forwarders to 168.63.129.16 | 6 |
| Agent can't reach AI Search | Missing PE or DNS link for `privatelink.search.windows.net` | 5, 7 |
| Storage unreachable (no PE, no bypass) | No PE, no shared PE, and no AzureServices bypass on storage | 7 |
| Shared PE pending | Resource access rule exists but PE needs approval on target | 7 |
| AI Search shared PE pending | AI Search outbound shared PE needs approval | 7 |
| Caphost stuck in Creating | RBAC not assigned before caphost creation | 9, 11 |
| Agent can't store threads | Missing Cosmos DB SQL data-plane role | 11 |
| Agent can't write files | Missing Storage Blob Data Owner with ABAC condition | 11 |
| Deployment blocked by policy | Azure Policy with Deny effect on resource config | 15 |

## BYO (Bring Your Own) Resources in Other Resource Groups

The script auto-discovers BYO resources that live outside the primary `ResourceGroup`:

1. **VNet**: Extracted from the network injection `subnetArmId` — if the subnet is in a different RG, the VNet is included in sections 3, 4, and 6 automatically.
2. **Storage, Cosmos DB, AI Search**: Discovered by parsing project connection target URLs and looking up the resource subscription-wide. Automatically included in sections 7, 12, and 13.
3. **DNS Zones**: Searched in both the primary RG and any BYO RGs discovered above.

No extra parameters are needed. The script logs `[INFO] BYO <type> '<name>' discovered in RG '<rg>'` for each cross-RG resource it finds.

> **Limitation**: Resources in a different *subscription* are not auto-discovered. If you have cross-subscription BYO resources, those sections will show `[WARN]` and you'll need to check them manually.

## Applies To

Works with all Foundry private network setups (Bicep and Terraform).
