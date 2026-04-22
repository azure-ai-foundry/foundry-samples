<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# LangGraph Toolbox Agent — azd Quick Start Guide

A LangGraph ReAct agent connected to an **toolbox in Microsoft Foundry** via MCP, with a
complete `azd` workflow for init, provision, and deploy.

## How It Works

The agent (`main.py`) works exactly like the [LangGraph sample](../langgraph/) but is
structured for `azd` deployment:

1. On startup, `quickstart()` calls `client.get_tools()` against the Toolbox MCP endpoint.
2. A `create_react_agent` is built from the loaded tools and an Azure OpenAI LLM.
3. `ResponsesAgentServerHost` serves requests on port `8088`.
4. OAuth consent errors (MCP code `-32006`) are caught and surfaced to the caller as a
   fallback tool message — the agent doesn't crash.

The `azd/` folder includes:
- `main.py` — LangGraph agent (same pattern as `../langgraph/`)
- `agent.yaml` — azd manifest for the default (GitHub MCP) scenario
- `agent.manifest.yaml` — agent manifest for `azd ai agent init -m` for the default scenario
- `azd-samples/` — scenario-specific `agent.yaml` + `agent.manifest.yaml` for 14 toolbox scenarios

## Prerequisites

- Python 3.12+
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) (`azd`)
- `azure.ai.agents` azd extension installed
- A Microsoft Foundry account and project
- `az login` completed
- **Owner** or **User Access Administrator** role on the subscription or resource group — `azd provision` creates RBAC role assignments; without this, provision appears to succeed but `azd ai agent invoke` fails with **424 PermissionDenied**

> **No pre-created toolbox needed.** `azd deploy` creates the toolbox automatically in your Foundry project (along with the container image and agent version). You don't need to run `sample_toolboxes_crud.py` or create a toolbox manually before using this sample.

## Setup

### 1. Install Azure Developer CLI (`azd`)

**Linux/macOS:**
```bash
curl -fsSL https://aka.ms/install-azd.sh | bash
```

**Windows (PowerShell):**
```powershell
winget install microsoft.azd
```

See the [full installation docs](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) for other options.

### 2. Install the AI Agents azd extension

```bash
azd extension install azure.ai.agents
```

To upgrade the extension later:

```bash
azd extension upgrade azure.ai.agents
```

### 3. Log in to Azure

```bash
azd auth login
```

### 4. Fix git CRLF setting (Windows only)

```bash
git config --global core.autocrlf false
```

## Quick Start (Deploy with azd)

> **IMPORTANT:** The `-m` (or `--manifest`) flag is **required** for `azd ai agent init`.
> It tells the command where to find your agent definition and source files.
>
> `-m` can point to either:
> - **A specific `agent.yaml` file** — init copies all files from the same directory as the manifest
> - **A folder containing `agent.yaml`** — init copies all files from that folder
>
> All files in the manifest directory (main.py, Dockerfile, requirements.txt, setup.py, etc.)
> are copied **verbatim** into the scaffolded project under `src/<agent-name>/`.

```powershell
# 1. Create a manifest directory with your agent.yaml + source files
mkdir my-agent/manifest
# Copy agent.yaml, main.py, Dockerfile, requirements.txt into my-agent/manifest/

# 2. Initialize the azd project (note: -m is REQUIRED)
cd my-agent
$PROJECT_ID = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>"
azd ai agent init -m https://raw.githubusercontent.com/microsoft/hosted-agents-vnext-private-preview/main/samples/python/toolbox/azd/agent.manifest.yaml --project-id $PROJECT_ID -e my-env
# Or equivalently: azd ai agent init -m manifest/ --project-id $PROJECT_ID -e my-env
# ↑ If your agent.yaml declares {{ param }} secrets (e.g., github_pat), you will be prompted to enter
#   them interactively HERE — before init completes. This is the only safe time to supply credentials.
# NOTE: Do NOT use --no-prompt here — it skips the prompt and leaves {{ param }} credentials empty (see Troubleshooting: Credentials Empty with --no-prompt)

# 3. CRITICAL post-init fixes (see "Post-Init Checklist" below)
azd env set enableHostedAgentVNext "true" -e my-env
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME "gpt-4o" -e my-env  # must match the deployment name in azure.yaml

# 4. Provision infrastructure (creates connections via Bicep)
azd provision -e my-env

# 5. Deploy agent (creates toolboxes, container image, agent version)
azd deploy -e my-env

# 6. Invoke the agent (MUST run from the scaffolded project directory)
azd ai agent invoke --new-session "Hello, what tools do you have?" --timeout 120
```

### Post-Init Checklist

After `azd ai agent init`, you **must** perform these steps before provision/deploy will work:

| # | Action | Why |
|---|--------|-----|
| 1 | `azd env set enableHostedAgentVNext "true"` | Without this, container health probes fail |
| 2 | Edit `src/<agent>/agent.yaml`: replace all `${{VAR}}` with `${VAR}` | Init scaffolds broken double-brace syntax that is NOT resolved at deploy time |
| 3 | Verify `agent.yaml` uses **flat format** (`kind: hosted` at root) | The nested `template:` format silently fails during deploy |
| 4 | For connections with credentials (e.g., GitHub PAT): enter them at the interactive `azd ai agent init` prompt. If you used `--no-prompt`, set them directly in `azure.yaml` under `config.connections[].credentials.keys` | Do NOT use `azd env set` for JSON credential values — causes unmarshal errors (see [Credentials Empty with --no-prompt](#-param--credentials-empty-with---no-prompt-e2e-validated)) |
| 5 | Verify `main.py` checks `FOUNDRY_PROJECT_ENDPOINT` first | Platform injects this var, NOT `AZURE_AI_PROJECT_ENDPOINT` |
| 6 | `azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME "<deployment-name>"` | Must match the deployment `name` in `azure.yaml`; platform injection is unreliable without this (container crashes on startup) |
| 7 | **If using existing project with AppInsights already connected:** `azd env set ENABLE_MONITORING "false"` | Provision fails with duplicate App Insights connection error |
| 8 | **If model region ≠ RG region:** edit generated `infra/main.parameters.json` — change `aiDeploymentsLocation` value from `${AZURE_LOCATION}` to `${AZURE_AI_DEPLOYMENTS_LOCATION}`, then `azd env set AZURE_AI_DEPLOYMENTS_LOCATION "<region>"` | Init templates map model deployment location to `AZURE_LOCATION` which is wrong when model is in a different region |

### What `azd ai agent init` Does

`azd ai agent init` copies all source files (main.py, Dockerfile, requirements.txt, etc.) **verbatim** from the manifest directory into `src/<agent-name>/` in the scaffolded project. It does NOT generate or modify main.py — it copies the exact file from your manifest.

The init command also:
- Creates `azure.yaml` with service config, connections, and toolbox definitions
- Creates `infra/` directory with Bicep templates
- Creates `.azure/<env>/.env` with environment variables

### Invoke Must Run from Scaffolded Directory

`azd ai agent invoke` reads the `azure.yaml` and `.azure/<env>/` configuration from the **current working directory**. If you run it from a different directory, it fails with "no project found."

```powershell
# WRONG — will fail
cd C:\some\other\dir
azd ai agent invoke --new-session "Hello" --timeout 120

# CORRECT — run from the scaffolded project
cd C:\Users\me\AppData\Local\Temp\my-scaffolded-project
azd ai agent invoke --new-session "Hello" --timeout 120
```

The `--timeout 120` flag is recommended because agent cold starts can take up to 60 seconds.

---

## Project Structure

After `azd ai agent init`, you get:

```
my-project/
├── .azure/
│   └── <env-name>/
│       ├── .env              # Environment variables (auto-populated by azd)
│       └── config.json       # Subscription + location config
├── .github/                  # CI/CD workflows
├── infra/
│   ├── main.bicep            # Top-level Bicep template
│   ├── main.parameters.json  # Parameters (references .env values)
│   └── core/
│       └── ai/
│           ├── ai-project.bicep  # Project + connection deployment
│           └── connection.bicep  # Connection resource template
├── src/
│   └── <agent-name>/
│       ├── agent.yaml       # Agent definition (env vars, protocols)
│       ├── main.py          # Your agent code
│       ├── Dockerfile       # Container build
│       ├── requirements.txt # Azure SDK preview packages
│       └── requirements-pypi.txt  # Public packages
├── azure.yaml               # azd service + toolbox configuration
└── .gitignore
```

### Key Files

| File | Purpose |
|------|---------|
| `azure.yaml` | Defines services, connections, toolboxes, model deployments |
| `src/<agent>/agent.yaml` | Agent manifest: name, env vars, protocols, resources |
| `.azure/<env>/.env` | Environment variables consumed by Bicep and Go extension |
| `infra/core/ai/connection.bicep` | Bicep template for creating connections |

---

## azure.yaml Structure

```yaml
requiredVersions:
  extensions:
    azure.ai.agents: '>=0.1.0-preview'
name: my-agent-project
services:
  my-agent:
    project: ./src/my-agent
    host: azure.ai.agent
    language: docker
    docker:
      remoteBuild: true
    config:
      container:
        resources:
          cpu: "0.25"
          memory: 0.5Gi
      deployments:                    # Model deployments
        - model:
            format: OpenAI
            name: gpt-4o
            version: "2024-11-20"
          name: gpt-4o
          sku:
            capacity: 10
            name: GlobalStandard
      connections:                    # Connection definitions
        - name: my-connection
          category: RemoteTool        # or CognitiveSearch, RemoteA2A, etc.
          authType: CustomKeys
          target: https://example.com
          credentials:
            keys:
              Authorization: "Bearer my-token"
      toolboxes:                      # Toolbox definitions
        - name: my-tools
          description: My tool collection
          tools:
            - type: web_search
            - type: mcp
              server_label: my-mcp
              project_connection_id: my-connection
infra:
  provider: bicep
  path: ./infra
```

---

## agent.yaml Structure

The agent.yaml **MUST** use the flat format with `kind: hosted` at the top level. The nested `template:` format (where `kind` is under `template.kind`) is silently ignored by the Go extension during deploy.

```yaml
# CORRECT — flat format (required)
kind: hosted
name: my-agent
description: My agent description
metadata:
  tags:
    - AI Agent Hosting
protocols:
  - protocol: responses
    version: 1.0.0
environment_variables:
  # FOUNDRY_PROJECT_ENDPOINT and FOUNDRY_AGENT_TOOLBOX_* are injected
  # automatically by the platform at runtime — do NOT declare them here.
  - name: AZURE_OPENAI_ENDPOINT
    value: ${AZURE_OPENAI_ENDPOINT}
  - name: AZURE_AI_MODEL_DEPLOYMENT_NAME
    value: ${AZURE_AI_MODEL_DEPLOYMENT_NAME}
resources:
  - kind: model
    id: gpt-4o
    name: chat
```

```yaml
# WRONG — nested format (deploy silently fails)
name: my-agent
template:
  kind: hosted     # ← This is NOT read by the Go extension
  protocols: [...]
```

> **CRITICAL**: Use `${VAR}` (single-brace) syntax for environment variables in agent.yaml.  
> The `${{VAR}}` (double-brace) syntax that `azd init` scaffolds is **NOT resolved** by the Go extension — the container receives the literal string `${{VAR}}`.
>
> **CRITICAL**: Use the **flat format** (with `kind:` at top level) — NOT the nested `template:` format. The Go extension requires `kind: hosted` at the top level.

---

## Supported Scenarios

Each scenario in [`azd-samples/`](azd-samples/) ships its own `agent.yaml` and `agent.manifest.yaml`. To use a scenario:

> ⚠️ **Important:** The `azd-samples/` subdirectories contain **only YAML manifests** (`agent.yaml` + `agent.manifest.yaml`). They do **not** include source code (`main.py`, `Dockerfile`, `requirements.txt`, etc.). When you init from a scenario manifest, `azd ai agent init` copies source files from the **manifest directory** — so copy the scenario's YAML files into the root `azd/` folder first to pick up the existing `main.py` and other source files.

1. Copy `agent.yaml` and `agent.manifest.yaml` from the scenario folder into `toolbox/azd/` (replacing the defaults).
2. Run `azd ai agent init -m agent.manifest.yaml` — this **auto-generates** `azure.yaml` from the manifest.
3. Set any required env vars (see table), then run `azd provision` and `azd deploy`.

| # | Scenario | Sample | Required env vars | Notes |
|---|----------|--------|-------------------|-------|
| 1 | Web Search | [`azd-samples/web-search/`](azd-samples/web-search/) | — | No connection needed |
| 2 | File Search | [`azd-samples/file-search/`](azd-samples/file-search/) | `FILE_SEARCH_VECTOR_STORE_ID` | Vector store must be in same project |
| 3 | Code Interpreter | [`azd-samples/code-interpreter/`](azd-samples/code-interpreter/) | — | Requires active model deployment at runtime |
| 4 | MCP Key-Auth (GitHub) | [`azd-samples/mcp-keyauth/`](azd-samples/mcp-keyauth/) | `GITHUB_PAT` | `server_label` is required |
| 5 | MCP No-Auth | [`azd-samples/mcp-noauth/`](azd-samples/mcp-noauth/) | — | Public MCP servers only |
| 6 | MCP OAuth (Managed Connector) | [`azd-samples/mcp-oauth-managed/`](azd-samples/mcp-oauth-managed/) | — | First call returns consent URL (code `-32006`) |
| 7 | MCP OAuth (Custom App) | [`azd-samples/mcp-oauth-custom/`](azd-samples/mcp-oauth-custom/) | — | First call returns consent URL (code `-32006`) |
| 8 | MCP Agent Identity | [`azd-samples/mcp-agent-identity/`](azd-samples/mcp-agent-identity/) | — | Assign RBAC role to agent identity first |
| 9 | Azure AI Search | [`azd-samples/ai-search/`](azd-samples/ai-search/) | `AI_SEARCH_ENDPOINT`, `AI_SEARCH_KEY` | `CognitiveSearch` connection |
| 10 | A2A (Agent-to-Agent) | [`azd-samples/a2a/`](azd-samples/a2a/) | — | Tool name prefixed with toolbox name |
| 11 | Bing Custom Search | [`azd-samples/bing-custom-search/`](azd-samples/bing-custom-search/) | `BING_API_KEY`, `BING_RESOURCE_ID`, `BING_CUSTOM_INSTANCE` | Uses `web_search` type + `custom_search_configuration` |
| 12 | OpenAPI Key-Auth | [`azd-samples/openapi-keyauth/`](azd-samples/openapi-keyauth/) | `TRIPADVISOR_API_KEY` | `openapi` key requires `name`, `spec`, `auth` sub-keys |
| 13 | MCP OAuth (Entra Passthrough) | [`azd-samples/mcp-entra-passthrough/`](azd-samples/mcp-entra-passthrough/) | — | `audience` field required; proxies user Entra token |
| 14 | Multi-Tool Toolbox | [`azd-samples/multi-tool/`](azd-samples/multi-tool/) | — | Combine multiple tool types in one toolbox |

---

## Troubleshooting

> Items marked **[E2E-Validated]** were confirmed as blockers during April 2026 from-scratch `azd` testing (init → provision → deploy → invoke).

### `${{VAR}}` double-brace syntax in agent.yaml

**Symptom:**
- `azd ai agent init` generates `${{VAR}}` in `src/<agent>/agent.yaml`.
- At runtime, the container receives the literal string `${{VAR}}` instead of the resolved value.

**Fix:** After init, replace all `${{VAR}}` with `${VAR}` (single-brace) in `src/<agent>/agent.yaml`.

### `azd ai agent invoke` fails with 424 PermissionDenied

**Symptom:** `azd provision` completes successfully but `azd ai agent invoke` returns `424 PermissionDenied`.  
**Root Cause:** `azd provision` creates RBAC role assignments on the Foundry project. This requires **Owner** or **User Access Administrator** on the subscription or resource group. If you only have Contributor, the Bicep deployment succeeds (role assignment resource is silently dropped) but the agent identity lacks the necessary permissions.  
**Fix:** Ensure you have the **Owner** or **User Access Administrator** role before running `azd provision`, then re-run `azd provision` to create the missing role assignments.

### Provision fails with "Only 100 connections are allowed"

**Symptom:** `azd provision` returns 400 for connection creation.  
**Fix:** The account has a 100-connection limit. Delete unused connections:
```powershell
$armToken = az account get-access-token --query accessToken -o tsv
$url = "https://management.azure.com<project-resource-id>/connections/<conn-name>?api-version=2025-04-01-preview"
Invoke-WebRequest -Method DELETE -Uri $url -Headers @{Authorization="Bearer $armToken"}
```

### Provision hangs on connection deployment

**Symptom:** `azd provision` stalls for 30+ minutes on connection creation.  
**Cause:** A previous deployment with the same connection name may be stuck.  
**Fix:** Cancel the deployment via ARM API:
```powershell
$armToken = az account get-access-token --query accessToken -o tsv
$url = "https://management.azure.com/subscriptions/<sub>/resourcegroups/<rg>/providers/Microsoft.Resources/deployments/connection-<name>/cancel?api-version=2021-04-01"
Invoke-RestMethod -Method POST -Uri $url -Headers @{Authorization="Bearer $armToken"}
```

### JSON escaping errors during provision

**Symptom:** "Failed to unmarshal" errors for connection credentials.  
**Fix:** Don't manually `azd env set` JSON values. Put credentials in `azure.yaml` `config.connections` and let the Go extension handle serialization. If you must inject manually, triple-escape: `\\\"` not `\"`.

### MAF `MCPStreamableHTTPTool` CancelledError [E2E-Validated]

**Symptom:** Agent returns "Request was cancelled" for every query.  
**Root Cause:** The MAF `MCPStreamableHTTPTool` has an `asyncio.CancelledError` bug in its async context manager that causes tool calls to silently cancel.  
**Fix:** Use the **LangGraph** agent pattern (with `langchain-mcp-adapters` `MultiServerMCPClient`) instead of MAF's `MCPStreamableHTTPTool`. This is why the `azd/main.py` sample uses LangGraph.

### Platform Env Var Names Don't Match Code Defaults [E2E-Validated]

**Symptom:** Container crashes with `ValueError: AZURE_AI_PROJECT_ENDPOINT environment variable must be set`.  
**Root Cause:** The platform injects `FOUNDRY_PROJECT_ENDPOINT` (not `AZURE_AI_PROJECT_ENDPOINT`) and `AZURE_AI_MODEL_DEPLOYMENT_NAME` (not `MODEL_DEPLOYMENT_NAME`).  
**Fix:** Always check the platform-injected name first, with a fallback:
```python
# CORRECT
PROJECT_ENDPOINT = os.getenv("FOUNDRY_PROJECT_ENDPOINT") or os.getenv("AZURE_AI_PROJECT_ENDPOINT")
MODEL = os.getenv("AZURE_AI_MODEL_DEPLOYMENT_NAME") or os.getenv("MODEL_DEPLOYMENT_NAME", "gpt-4o")
```

### Stale Toolbox Connection Reference [E2E-Validated]

**Symptom:** Agent loads 0 tools ("no tools available") even though the toolbox exists.  
**Root Cause:** After reprovisioning, the toolbox may still reference an old/deleted connection name (e.g., `github-mcp-connection` vs. `github-mcp-conn`). The `azd provision` creates the connection but doesn't update the toolbox's `project_connection_id`.  
**Fix:** Ensure the connection `name` in `azure.yaml` exactly matches the `project_connection_id` in the toolbox config. If they're out of sync, delete the toolbox and redeploy:
```powershell
# Delete stale toolbox
$token = az account get-access-token --resource "https://ai.azure.com" -o tsv --query accessToken
curl -X DELETE "$ENDPOINT/toolsets/<toolbox-name>?api-version=v1" -H "Authorization: Bearer $token" -H "Foundry-Features: Toolsets=V1Preview"
# Then run azd deploy again
azd deploy -e <env> --no-prompt
```

### `{{ param }}` Credentials Empty with `--no-prompt` [E2E-Validated]

**Symptom:** Connection created with empty/invalid credentials; MCP returns "Authorization header is badly formatted".  
**Root Cause:** The `{{ github_pat }}` template syntax in the manifest is resolved **interactively** at `azd ai agent init` time. With `--no-prompt`, secret parameters get empty values.  
**Fix:** After init with `--no-prompt`, manually set the credential in `azure.yaml`:
```yaml
# In azure.yaml, under config.connections:
credentials:
  keys:
    Authorization: "Bearer ghp_xxxxx"  # Your actual PAT
```
Or set the env var before provision: `azd env set GITHUB_PAT "ghp_xxxxx"`

### Toolbox MCP Endpoint Auto-Injection [E2E-Validated]

**Symptom:** Agent code can't find the toolbox endpoint.  
**Root Cause:** The platform auto-injects `TOOLBOX_{TOOLBOX_NAME}_MCP_ENDPOINT` (e.g., `TOOLBOX_AGENT_TOOLS_MCP_ENDPOINT` for a toolbox named `agent-tools`). The variable name is derived from the toolbox name in `azure.yaml`, with dashes converted to underscores and uppercased.  
**Fix:** Agent code should check `TOOLBOX_{NAME}_MCP_ENDPOINT` first, then fall back to constructing the URL from `FOUNDRY_AGENT_TOOLBOX_ENDPOINT`. See the `main.py` sample for the correct pattern.

### Connection Names Must Use Dashes, Not Underscores [E2E-Validated]

**Symptom:** Bicep deployment fails on connection name validation.  
**Fix:** Connection names in `azure.yaml` must use only alphanumeric characters, dashes, and dots. Replace underscores with dashes (e.g., `github-mcp-conn` not `github_mcp_conn`).

### Connection `category` Must Be `RemoteTool` (Not `CustomKeys`) [E2E-Validated]

**Symptom:** `azd provision` fails with "Object reference not set to an instance of an object" when creating a connection.  
**Root Cause:** The `category` field specifies the connection *type* (e.g., `RemoteTool`, `CognitiveSearch`, `RemoteA2A`). Using `CustomKeys` as the `category` (confusing it with the `authType`) causes a null reference in the provisioning pipeline.  
**Fix:** Always use `category: RemoteTool` for MCP or HTTP tool connections that authenticate with API keys. `CustomKeys` belongs in `authType` (in azure.yaml) or `credentials.type` (in agent.yaml), never in `category`:
```yaml
# CORRECT
connections:
  - name: github-mcp-conn
    category: RemoteTool        # ✅ connection TYPE — not "CustomKeys"
    authType: CustomKeys        # ✅ auth METHOD — this is where CustomKeys goes
    target: https://api.githubcopilot.com/mcp
    credentials:
      keys:
        Authorization: "Bearer {{ github_pat }}"

# WRONG
connections:
  - name: github-mcp-conn
    category: CustomKeys        # ❌ causes "Object reference not set" during provision
```

### Provision Fails with Duplicate App Insights Connection [E2E-Validated]

**Symptom:** `azd provision` fails with a deployment error about a duplicate Application Insights connection on the AI project.  
**Root Cause:** The Bicep template tries to create an App Insights connection on the AI project, but one already exists (created earlier by the portal, another deployment, or a prior `azd provision` run).  
**Fix:** Set `ENABLE_MONITORING=false` before provisioning to skip the App Insights connection deployment:
```powershell
azd env set ENABLE_MONITORING "false" -e my-env
azd provision -e my-env
```

### `aiDeploymentsLocation` Template Bug (Model Region Mismatch) [E2E-Validated]

**Symptom:** `azd provision` fails with `InvalidResourceLocation` on the AI account or model deployment, even though `AZURE_LOCATION` is set correctly.  
**Root Cause:** The `infra/main.parameters.json` generated by `azd ai agent init` maps `aiDeploymentsLocation` to `${AZURE_LOCATION}`. If your resource group is in one region (e.g., `westus2`) but your AI account and model deployment are in another (e.g., `northcentralus`), provision fails because it tries to deploy the model to the wrong region.  
**Fix:** After `azd ai agent init`, manually edit `infra/main.parameters.json`:
```json
// Change this (generated by init — WRONG when model region ≠ RG region):
"aiDeploymentsLocation": {
  "value": "${AZURE_LOCATION}"
}
// To this:
"aiDeploymentsLocation": {
  "value": "${AZURE_AI_DEPLOYMENTS_LOCATION}"
}
```
Then set the model region:
```powershell
azd env set AZURE_AI_DEPLOYMENTS_LOCATION "northcentralus" -e my-env  # region where your AI account is
```

---

## Command Reference

| Command | Purpose |
|---------|---------|
| `azd ai agent init -m <manifest> --project-id <id> -e <env>` | Initialize azd project from manifest (omit `--no-prompt` for credential prompts) |
| `azd provision -e <env>` | Deploy infrastructure (connections via Bicep) |
| `azd deploy -e <env>` | Deploy agent (toolboxes + container + agent version) |
| `azd ai agent invoke --new-session "<msg>" --timeout 120` | Send message to deployed agent |
| `azd ai agent monitor --tail 50` | View agent container logs |
| `azd env set <KEY> "<VALUE>" -e <env>` | Set environment variable |
| `azd env get-values -e <env>` | List all environment variables |

---

## Verifying Toolbox via MCP API (Direct)

You can verify toolboxes without deploying an agent by calling the MCP endpoint directly:

```python
import httpx
from azure.identity import DefaultAzureCredential

cred = DefaultAzureCredential()
token = cred.get_token("https://ai.azure.com/.default").token
endpoint = "https://<account>.services.ai.azure.com/api/projects/<project>"
headers = {
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json",
    "Foundry-Features": "Toolboxes=V1Preview",
}

# Initialize
init = {"jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                   "clientInfo": {"name": "test", "version": "1.0"}}}
httpx.post(f"{endpoint}/toolboxes/<toolbox-name>/versions/<version>/mcp?api-version=v1",
           json=init, headers=headers, timeout=30)

# List tools
resp = httpx.post(f"{endpoint}/toolboxes/<toolbox-name>/versions/<version>/mcp?api-version=v1",
                  json={"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}},
                  headers=headers, timeout=60)
tools = resp.json()["result"]["tools"]
print(f"Tools ({len(tools)}):", [t["name"] for t in tools])

# Call a tool
resp = httpx.post(f"{endpoint}/toolboxes/<toolbox-name>/versions/<version>/mcp?api-version=v1",
                  json={"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                        "params": {"name": "web_search",
                                   "arguments": {"search_query": "test"}}},
                  headers=headers, timeout=120)
print(resp.json()["result"]["content"])
```

> **CRITICAL:** The MCP endpoint requires both the `Foundry-Features: Toolboxes=V1Preview` header AND the `?api-version=v1` query parameter. The URL format is `/toolboxes/<name>/versions/<version>/mcp?api-version=v1` where `<version>` is the toolbox version number (e.g., `1`).

---

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
