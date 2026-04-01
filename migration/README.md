# V1 → V2 Agent Migration Tool

Migrate classic Azure AI assistants (v1 `asst_...` items) to v2 named agents.

---

## Quickstart

### Step 1 — Download Docker Desktop

[Download Docker Desktop](https://www.docker.com/products/docker-desktop/)

Start it and wait for the whale icon to appear in your system tray.

### Step 2 — Install Azure CLI

Windows:

[Install Azure CLI for Windows](https://learn.microsoft.com/cli/azure/install-azure-cli-windows)

If `winget` is available, `migrate-docker.ps1` will try to install Azure CLI for you automatically when it is missing.

### Step 3 — Run

```powershell
cd migration

.\migrate-docker.ps1 --resource-id "<YOUR_RESOURCE_ID>" --list
```

That's it. No Python install and no `pip install`.  
The migration runtime stays inside Docker, but Azure sign-in happens on the Windows host via Azure CLI so corporate Conditional Access can validate your compliant device.

The `--list` flag is read-only — it shows what would be migrated without making any changes.  
When you're ready to migrate for real, drop `--list`:

```powershell
.\migrate-docker.ps1 --resource-id "<YOUR_RESOURCE_ID>"
```

---

## Where to find your Resource ID

**Azure Portal** → your AI Services resource → **Properties** → **Resource ID**

or **Foundry portal** → Project settings → **Resource ID**

```text
/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{resource}/projects/{project}
```

### Your current values (validated)

| | |
| --- | --- |
| **Full Resource ID** | `/subscriptions/47f1c914-e299-4953-a99d-3e34644cfe1c/resourceGroups/rg-nikhowlett-6102/providers/Microsoft.CognitiveServices/accounts/nikhowlett-6102-resource/projects/nikhowlett-6102` |
| Subscription | `47f1c914-e299-4953-a99d-3e34644cfe1c` |
| Tenant | `72f988bf-86f1-41af-91ab-2d7cd011db47` |
| Resource group | `rg-nikhowlett-6102` |
| Resource name | `nikhowlett-6102-resource` |
| Project name | `nikhowlett-6102` |
| Account | `nikhowlett@microsoft.com` |

Ready-to-run:

```powershell
.\migrate-docker.ps1 --resource-id "/subscriptions/47f1c914-e299-4953-a99d-3e34644cfe1c/resourceGroups/rg-nikhowlett-6102/providers/Microsoft.CognitiveServices/accounts/nikhowlett-6102-resource/projects/nikhowlett-6102" --list
```

---

## RBAC — Required permissions

The signed-in user needs **Azure AI User** on the AI Services resource.  
Without this role you will see `401` or `403` errors.

### Check your current roles

```powershell
az role assignment list --assignee "nikhowlett@microsoft.com" `
  --scope "/subscriptions/47f1c914-e299-4953-a99d-3e34644cfe1c/resourceGroups/rg-nikhowlett-6102/providers/Microsoft.CognitiveServices/accounts/nikhowlett-6102-resource" `
  -o table
```

### Grant the role (requires Owner or User Access Admin on the resource)

```powershell
az role assignment create `
  --role "Azure AI User" `
  --assignee "nikhowlett@microsoft.com" `
  --scope "/subscriptions/47f1c914-e299-4953-a99d-3e34644cfe1c/resourceGroups/rg-nikhowlett-6102/providers/Microsoft.CognitiveServices/accounts/nikhowlett-6102-resource"
```

📖 [Foundry RBAC docs](https://learn.microsoft.com/azure/ai-foundry/concepts/rbac-ai-foundry)

---

## Common commands

```powershell
# List everything (read-only)
.\migrate-docker.ps1 --resource-id "<ID>" --list

# Migrate ALL items
.\migrate-docker.ps1 --resource-id "<ID>"

# Migrate only items with tools (bing, code_interpreter, etc.)
.\migrate-docker.ps1 --resource-id "<ID>" --only-with-tools

# Migrate only plain assistants (no tools)
.\migrate-docker.ps1 --resource-id "<ID>" --only-without-tools

# Migrate one item by ID
.\migrate-docker.ps1 --resource-id "<ID>" asst_abc123def456

# Cross-project: read from one project, write to another
.\migrate-docker.ps1 --source-resource-id "<SOURCE_ID>" --resource-id "<TARGET_ID>"
```

---

## What `--list` actually shows

The tool checks **two separate backends** — the Foundry portal stores v1 items in two different places depending on how they were created:

| Portal page | Endpoint queried |
| --- | --- |
| **Agents** page | `{resource}.services.ai.azure.com/api/projects/{project}/assistants` |
| **Assistants** page | `{resource}.cognitiveservices.azure.com/openai/assistants` |

All `asst_...` items on either endpoint are v1 and need migration.  
Real v2 agents are named (`agent.name = "MyAgent"`, `agent.version = "1"`) — `--list` will never show those.

---

## Don't have Docker? Use migrate.ps1 instead

If you already have **Python 3.10+** and **Azure CLI** installed locally, you can skip Docker:

```powershell
pip install -r requirements.txt   # one-time

.\migrate.ps1 --resource-id "<ID>" --list
.\migrate.ps1 --resource-id "<ID>"
```

`migrate.ps1` handles Azure login automatically — if you're not signed in it starts device-code flow.

Linux / macOS: use `./migrate.sh` with the same flags.

---

## Troubleshooting

| Error | Fix |
| --- | --- |
| `failed to connect to the docker API` | Docker Desktop is not running — start it and wait for the whale icon |
| `AADSTS53003` or `You don't have access to this` | Corporate Conditional Access blocked sign-in inside Linux containers — use host Azure CLI login; `migrate-docker.ps1` now does this automatically |
| `az : The term 'az' is not recognized` | Install Azure CLI, or rerun `migrate-docker.ps1` on Windows and let it install Azure CLI with `winget` |
| `401 Unauthorized` | Check RBAC — you need **Azure AI User** on the resource (see above) |
| `403 Forbidden` | Assign **Azure AI User** role (see RBAC section above) |
| `Could not switch to subscription` | Run `az login --tenant 72f988bf-86f1-41af-91ab-2d7cd011db47` |
| Items missing from `--list` | Both endpoints are checked; if still missing the item may have been deleted |
| Docker Desktop crashes on startup | Delete `%USERPROFILE%\.docker\contexts\meta` and restart |

---

## Files in this folder

| File | Purpose |
| --- | --- |
| `migrate-docker.ps1` | **Main entry point** — Docker wrapper, no local Python needed |
| `migrate.ps1` | Local Python alternative (requires Python + Azure CLI) |
| `migrate.sh` | Bash version of `migrate.ps1` |
| `v1_to_v2_migration.py` | Core migration engine |
| `requirements.txt` | Python deps (baked into the Docker image, also used by `migrate.ps1`) |
| `Dockerfile` | Container: Python 3.11 + Azure CLI + requirements |
| `entrypoint-login.sh` | Helper entrypoint for Azure-auth scenarios inside the container |
