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

### Example values

| | |
| --- | --- |
| **Full Resource ID** | `/subscriptions/<your-subscription-id>/resourceGroups/<your-resource-group>/providers/Microsoft.CognitiveServices/accounts/<your-resource-name>/projects/<your-project-name>` |
| Subscription | `<your-subscription-id>` |
| Tenant | `<your-tenant-id>` |
| Resource group | `<your-resource-group>` |
| Resource name | `<your-resource-name>` |
| Project name | `<your-project-name>` |
| Account | `<your-account@example.com>` |

Ready-to-run:

```powershell
.\migrate-docker.ps1 --resource-id "/subscriptions/<your-subscription-id>/resourceGroups/<your-resource-group>/providers/Microsoft.CognitiveServices/accounts/<your-resource-name>/projects/<your-project-name>" --list
```

---

## RBAC — Required permissions

The signed-in user needs **Foundry User** on the AI Services resource.  
Without this role you will see `401` or `403` errors.

### Check your current roles

```powershell
az role assignment list --assignee "<your-account@example.com>" `
  --scope "/subscriptions/<your-subscription-id>/resourceGroups/<your-resource-group>/providers/Microsoft.CognitiveServices/accounts/<your-resource-name>" `
  -o table
```

### Grant the role (requires Owner or User Access Admin on the resource)

```powershell
az role assignment create `
  --role "Foundry User" `
  --assignee "<your-account@example.com>" `
  --scope "/subscriptions/<your-subscription-id>/resourceGroups/<your-resource-group>/providers/Microsoft.CognitiveServices/accounts/<your-resource-name>"
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

# Migrate agent definitions ONLY, skip files/vector stores (advanced — see below)
.\migrate-docker.ps1 --resource-id "<ID>" --no-migrate-files
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

## Files & vector stores

When an assistant uses **`file_search`** or **`code_interpreter`**, its uploaded files and vector stores live in the *source* project. Those IDs don't exist in the target project, so a naive copy would leave the migrated agent with broken references. The tool handles this for you.

### It's automatic — no flag required

File and vector-store migration is **on by default**. For every assistant it migrates, the tool:

1. Reads the tool resources on the v1 assistant.
2. **Downloads** each referenced file from the source project.
3. **Re-uploads** it to the target project's **Foundry endpoint** (`{resource}.services.ai.azure.com`).
4. **Recreates** each `file_search` vector store on the target and attaches the re-uploaded files.
5. **Rewrites** the file IDs and vector-store IDs in the new v2 agent definition so everything resolves.

You'll see it in the run output:

```text
📂 File migration: ENABLED (target Foundry endpoint: https://<resource>.services.ai.azure.com/api/projects/<project>)
   📦 Migrating files for assistant asst_abc123
   🔧 code_interpreter: 2 file(s) to migrate
   📥 Downloaded sales.csv (10240B)
   📤 Uploaded sales.csv -> assistant-file-...
   🔍 file_search: 1 vector store(s) to migrate
   🗄️  Created vector store vs_...
   ✅ Vector store vs_... ready (3 file(s) ingested)
```

### What gets migrated (and what doesn't)

| Tool on the v1 assistant | What's migrated | Read from |
| --- | --- | --- |
| `code_interpreter` | Each attached file | `tool_resources.code_interpreter.file_ids` |
| `file_search` | Every file inside each vector store, plus a freshly created vector store | `tool_resources.file_search.vector_store_ids` |
| Any other tool (bing, function, openapi, …) | Nothing — these carry no files | — |

- **Only files referenced by a tool are copied.** Stray files in the project that no assistant points at are left alone.
- **No file-type or size filtering by this tool.** Whatever the source returns is re-uploaded as-is; the target Files / `file_search` API enforces the actual limits — currently a 512 MB max file size, 5,000,000 max tokens per file, and [these supported file types](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/file-search#supported-file-types) (`.c`, `.cpp`, `.cs`, `.doc`/`.docx`, `.html`, `.java`, `.json`, `.md`, `.pdf`, `.php`, `.pptx`, `.py`, `.rb`, `.tex`, `.txt`, `.css`, `.js`, `.sh`, `.ts` — text files must be UTF-8/UTF-16/ASCII). A vector store can also hold at most 10,000 files. If a file is too large or an unsupported type, the API rejects it (HTTP 400/415, or the vector-store file status ends up `failed`) — the tool does **not** retry these, and reports the file id + API-provided reason (e.g. `[invalid_file] ...`) in the terminal, the assistant's `failed_files` list, and `migration_failed_files.json`.
- **Files shared** between `code_interpreter` and a vector store are uploaded once and reused (deduplicated).

### Turning it OFF — `--no-migrate-files`

Pass `--no-migrate-files` to copy **agent definitions only**. The v2 agent keeps the *source* file / vector-store IDs, which do **not** exist on the target — so `file_search` and `code_interpreter` stay broken until you re-upload manually. Use this only when the target already has the files, or when you plan to fix references yourself.

```powershell
.\migrate-docker.ps1 --resource-id "<ID>" --no-migrate-files
```

### Requirements & caveats

- The tool needs a reachable **source endpoint** and a derivable **target Foundry endpoint** (derived automatically from `--resource-id`). If either is missing it prints `⚠️  Cannot migrate files: …` and continues with definitions only.
- **A wrong `--production-endpoint` is accepted, not rejected.** If you override the target with a raw `--production-endpoint` that points at a legacy OpenAI-compatible host (`*.openai.azure.com`) or a raw Cognitive Services host (`*.cognitiveservices.azure.com`) instead of a Foundry host (`*.services.ai.azure.com`), the migration **will still run and the file/vector-store upload calls will still succeed (HTTP 200)** — but the uploaded files land in a namespace the Foundry agent runtime can't see, so `file_search`/`code_interpreter` stay broken on the migrated agent. The tool cannot tell this happened from the API responses alone, so it proactively checks the endpoint's hostname and prints a `⚠️` warning up front and again in the final summary — it does **not** fail the migration, since you may not care about files at all (see `--no-migrate-files`). Re-run with a Foundry-format endpoint to actually get working files.
- **Vector-store ingestion is polled per file, not per store.** Each file is attached to the vector store and polled individually (default: 30 attempts × 2s = up to 60s per file; configurable via `MIGRATION_VS_FILE_POLL_ATTEMPTS` / `MIGRATION_VS_FILE_POLL_INTERVAL`) until it reaches `completed` or `failed`. This isolates one bad file from the rest — the vector store is still created and attached with whatever files succeeded, even if the poll window elapses on a slow file (it keeps indexing server-side) or a file permanently fails.
- You need write access (**Foundry User**) on the *target* resource — the same role required for the rest of the migration.

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
| `401 Unauthorized` | Check RBAC — you need **Foundry User** on the resource (see above) |
| `403 Forbidden` | Assign **Foundry User** role (see RBAC section above) |
| `Could not switch to subscription` | Run `az login --tenant <your-tenant-id>` |
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
