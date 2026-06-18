# Foundry Private Network Cleanup

`cleanup.ps1` safely tears down Foundry deployments with VNet injection. It handles the specific deletion order required to avoid stuck resources, orphaned locks, and subnet conflicts that make manual cleanup painful.

## Why This Script Exists

Deleting a Foundry private network deployment is **not** as simple as `az group delete`. The deployment creates capability hosts with service association links (SALs) on VNet subnets. If you delete the resource group directly:

- **Capability hosts must be deleted in order** — project-level first, then account-level. Deleting in the wrong order can leave the account in a failed state.
- **SALs block subnet reuse** — subnets with active SALs cannot be re-delegated or deleted. SAL cleanup happens asynchronously after caphost deletion and can take up to 24 hours.
- **Soft-deleted accounts block redeployment** — Cognitive Services accounts are soft-deleted for 48 hours. A new deployment with the same name will fail unless the old account is purged.

This script handles all of this automatically: discovers resources, deletes in the correct order, waits for SAL cleanup, and purges soft-deleted accounts.

## Important: Use the VNet Resource Group

The `--ResourceGroup` parameter must point to the resource group containing the **AI Foundry account, project, and VNet** — not the resource group with dependent resources (Search, Cosmos, Storage).

If your deployment uses multiple resource groups, the cleanup script only needs the one with the AI account and VNet. Dependent resources in other resource groups can be deleted with a simple `az group delete`.

## Prerequisites

- [PowerShell 7+](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell) (cross-platform)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) logged in with access to the target subscription
- Active subscription must be set: `az account set --subscription <subscription-id>`

## Usage

```bash
cd infrastructure/infrastructure-setup-bicep/deployment-tools/cleanup
```

**Always start with a dry run** to see what would be deleted before making changes:

```powershell
.\cleanup.ps1 -SubscriptionId "<subscription-id>" -ResourceGroup "<resource-group>" -DryRun
```

> [!IMPORTANT]
> Always run `-DryRun` first and review the discovered accounts/projects/caphosts before running cleanup without `-DryRun`.

When you're satisfied with the discovery output, run without `-DryRun`. The script will prompt for confirmation before deleting anything.

```powershell
.\cleanup.ps1 -SubscriptionId "<subscription-id>" -ResourceGroup "<resource-group>"
```

## Parameters

| Parameter | Required | Description |
|---|---|---|
| `-SubscriptionId` | Yes | Azure subscription ID |
| `-ResourceGroup` | Yes | Resource group containing the AI Foundry account, project, and VNet |
| `-AccountName` | No | Limit cleanup to a specific AI Services account. When omitted, all AIServices accounts in the RG are discovered and cleaned up. |
| `-DryRun` | No | Show what would be cleaned up without taking any action |
| `-SkipSalWait` | No | Skip waiting for SAL removal (faster but risky — subnet may not be reusable immediately) |
| `-DeleteRG` | No | Delete the resource group after cleanup. Not allowed with `-AccountName` (account-scoped cleanup must not delete the whole RG). |

When `-AccountName` is provided, active cleanup remains scoped to that account, while soft-deleted account purge remains RG-wide residue cleanup.

## What It Does

### Step 0: Discovery

Auto-discovers all resources in the resource group — no need to know account or project names:

- AI Foundry accounts (kind: `AIServices`)
- Projects under each account
- Capability hosts (project-level and account-level)
- VNet subnets with active service association links

After discovery, a summary is printed and you are prompted to confirm before proceeding (unless `-DryRun` is set).

### Step 1: Delete Project Capability Hosts

Deletes all project-level capability hosts first. This is required before account-level caphosts can be removed.

### Step 2: Delete Account Capability Hosts

Deletes account-level capability hosts. Handles async deletion with polling (up to 30 min timeout).

### Step 3: Delete Projects and Purge AI Accounts

Deletes all projects under each account first (accounts cannot be deleted while nested projects exist), then deletes and purges each AI Services account to prevent soft-delete name collisions on redeployment. Also checks for and purges any previously soft-deleted accounts in the resource group.

### Step 4: Wait for SAL Cleanup

Waits for service association links to be removed from subnets (up to 20 min). SAL removal happens asynchronously after caphost deletion. If SALs are still present after 20 minutes, the script warns you to check again later — backend cleanup can take up to 24 hours.

SAL waiting runs only for caphost-linked subnets discovered during cleanup. If none are discovered, SAL waiting is skipped with a warning.

### Step 5: Resource Group (optional)

If `-DeleteRG` is specified, initiates an async deletion of the resource group. Otherwise, prints the command for manual deletion.

## Examples

```powershell
# Dry run — see what would be cleaned up
.\cleanup.ps1 -SubscriptionId "xxxx" -ResourceGroup "my-foundry-rg" -DryRun

# Clean up a specific account only
.\cleanup.ps1 -SubscriptionId "xxxx" -ResourceGroup "my-foundry-rg" -AccountName "my-ai-account"

# Full cleanup including resource group deletion
.\cleanup.ps1 -SubscriptionId "xxxx" -ResourceGroup "my-foundry-rg" -DeleteRG

# Fast cleanup (skip SAL wait — subnet may not be immediately reusable)
.\cleanup.ps1 -SubscriptionId "xxxx" -ResourceGroup "my-foundry-rg" -SkipSalWait
```
