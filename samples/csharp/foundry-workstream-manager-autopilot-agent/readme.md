# 🤖 Workstream Manager Agent

> A Foundry A365 agent that tracks work items, provides workstream summaries, and operates in manager-only direct message mode.

**Note:** This agent will currently only respond in group chats if you @mention it.

---

## 📋 Prerequisites

**Note:** You must be enrolled in the [Frontier preview program](https://adoption.microsoft.com/en-us/copilot/frontier-program/) to publish a Foundry agent to Microsoft Agent 365.

Ensure you have the following installed:

| Requirement | Description |
|------------|-------------|
| [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) | Infrastructure deployment tool |
| [.NET 9.0 SDK](https://dotnet.microsoft.com/download) | Development framework |


### 🔐 Required Permissions

- **Owner** role on the Azure subscription
- **Azure AI User** or **Cognitive Services User** role at subscription or resource group level
- **Tenant Admin** role for organization-wide configuration

---

## 🤖 Agent Functionality

Before deploying, you can customize:
- **Agent instructions:** [AgentInstructions.cs](./src/workstream_manager_agent/AgentLogic/AgentInstructions.cs)
- **MCP tools:** [ToolManifest.json](./src/workstream_manager_agent/ToolingManifest.json) - [Learn more](https://learn.microsoft.com/en-us/microsoft-agent-365/tooling-servers-overview)
- **Grant users permission to directly message the agent** — Users must be granted permission to direct message the agent before they can interact with it.
- **Direct-message access control:** Managers can manage a per-digital-worker allowlist in Teams direct message using:
  - `/access list`
  - `/access add <user-object-id-or-upn-or-mention>`
  - `/access remove <user-object-id-or-upn-or-mention>`
  - `azd provision` now creates and wires Azure Table Storage for allowlist persistence across sessions (table defaults to `digitalworkerallowlist`).
  - Teams **group chats** use the same allowlist and only allow responses when every participant is manager-approved.
  - Customize `GroupChatUnauthorizedResponse` with placeholders `{Manager}`, `{UnauthorizedCount}`, and `{UnauthorizedParticipants}`.
- **Work item tracking:** The agent automatically tracks action items mentioned in conversation:
  - When the LLM identifies an action item, it calls `create_work_item` and the agent reacts with 📌
  - Items are stored in Azure Table Storage with owner, description, status, ETA, and changelog
  - Owner AAD object IDs are resolved automatically via Microsoft Graph
  - Tools available: `create_work_item`, `list_work_items`, `update_work_item`, `close_work_item`
- **Workstream summary:** `/workstreamsummary run` generates an on-demand summary of all open work items grouped by owner
- **Manager onboarding:** Run `/onboarding` to show setup guidance for `/access` commands
- **Cross-tenant access guard:** Set `CrossTenantUnauthorizedResponse` to customize the canned no-op response for users outside the digital worker tenant.

---

## 🚀 Quick Start

### Step 1: Authenticate

Login to your Azure tenant and authenticate with Azure Developer CLI:

Based on tenant security settings, sometimes just az login might be sufficient, sometimes one will need to login to each scope that is used in these scripts.

```powershell
# Login to Azure CLI
az login

az login --scope https://ai.azure.com/.default

az login --scope https://graph.microsoft.com//.default

az login --scope https://management.azure.com/.default
# Login to Azure Developer CLI
azd auth login
```

### Step 2: Deploy

> **📍 Region availability:** This sample uses [Foundry hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd). Your Foundry account and other resources must be in a region where hosted agents are available. At the time of writing, supported regions are:
>
> Australia East, Brazil South, Canada Central, Canada East, East US, East US 2, France Central, Germany West Central, Italy North, Japan East, Korea Central, North Central US, Norway East, Poland Central, South Africa North, South Central US, South India, Southeast Asia, Spain Central, Sweden Central, Switzerland North, UAE North, UK South, West Central US, West US, West US 3.

```powershell
azd provision
```

After deployment completes, retrieve your resource values:

```powershell
azd env get-values
```

### Step 3: Review and Publish the Agent Request

1. Navigate to the [Microsoft 365 admin center](https://admin.cloud.microsoft/?#/agents/all/requested)
2. Under **Requests**, locate your pending agent request:
   ![Find your pending agent request in A365](image.png)

3. Open the request and click **Publish to store**:
   ![Screenshot of the pending agent request details with the 'Publish to store' button highlighted](image-1.png)

### Step 4: Configure Teams Integration

After approving the agent blueprint, configure it in the Teams Developer Portal:

1. Open the [Teams Developer Portal](https://dev.teams.microsoft.com/tools/agent-blueprint) and locate your approved agent blueprint.
    
   **Note:** Only 100 Agent Blueprints are displayed. If yours isn't visible, click any blueprint to open its details page, then in the browser's address bar replace the blueprint ID portion of the URL with your own Blueprint ID from the previous step (for example: `https://dev.teams.microsoft.com/tools/agent-blueprint/<your-blueprint-id>`).
   ![Find agent blueprint](image-2.png)

2. Get your Blueprint ID:
   ```powershell
   azd env get-values
   ```

3. Navigate to **Configuration** and add your **Bot ID** (same as Blueprint ID):
   ![Screenshot showing the Bot ID configuration field in the Teams Developer Portal](image-3.png)

### Step 5: Create Agent Instances

After configuring the agent blueprint in Teams Developer Portal, you can now create agent instances based on your blueprint:

1. In Microsoft Teams, navigate to **Apps** → **Agents for your team**. Note: may only be available on Teams through browser.
2. Find the agent named by your `AGENT_NAME` value and create an instance:
   ```powershell
   azd env get-value AGENT_NAME
   ```
   ![Screenshot of Microsoft Teams showing the 'Agents for your team' section with an agent listed](image-4.png)

---

## 🔄 Updating the Agent After Code Changes

When you change the agent source code (anything under `src/`), re-run:

```powershell
azd provision
```

This re-runs the `postprovision` hook, which:

1. **Rebuilds and pushes the container image** via ACR Build (`scripts/build-docker-image-acr.ps1`).
2. **Registers a new agent version** that points at the freshly built image (`scripts/agent-creation-script.ps1`), polls until it is `active`, and re-applies the endpoint protocol/auth configuration.

Steps 1 and 2 run on **every** `azd provision`. The one-time digital worker setup steps — **publishing the digital worker**, creating the **blueprint SP OAuth2 grants**, and **adding you as blueprint owner** — are skipped on re-runs once they have completed (a `DIGITAL_WORKER_SETUP_DONE` marker is persisted in the azd environment). The published digital worker references the agent GUID, not a specific version, so new versions are served without re-publishing. You also do **not** need to recreate the blueprint or bot service — those are idempotent ARM resources.

To force the one-time setup steps to run again (e.g. after changing publish metadata or blueprint scopes):

```powershell
azd env set DIGITAL_WORKER_SETUP_DONE ""
azd provision
```

> **⚠️ Traffic routing & draining:** Creating a new agent version does not instantly move every live session onto it. When you shift endpoint traffic routing to the new version, **existing sessions continue to run on the previous version until they go idle**, so two versions can be active at once. Use the per-version telemetry queries in [Monitoring & Observability](#-monitoring--observability) (slice `requests` by `application_Version`) to watch the cutover and confirm when the old version has fully drained.

---

## 🔧 Deployment Reference: `/infra` + Post-Provision Scripts

`azd provision` runs in two phases: **(1)** it deploys the Bicep in `/infra`, then **(2)** it runs the `postprovision` hook (`scripts/post-provision.ps1`). The key distinction for permissions: `/infra` creates **managed-identity** role assignments (for the agent's runtime identities), while the post-provision **scripts run as *you*** and therefore require **your** user/directory roles.

### Phase 1 — `azd provision` deploys `/infra`

Creates the environment resources:

- **Foundry account** (Cognitive Services `AIServices`, system-assigned managed identity).
- **Foundry project** (child of the account, system-assigned managed identity).
- **Azure Container Registry** (ACR) — hosts the agent image.
- **Model deployment** (default: `gpt-5-chat`, version `2025-10-03`).
- **User-Assigned Managed Identity (UMI)** used to run a PowerShell **deployment script** that creates the **Agent Blueprint** (a dataplane operation). The blueprint is created here — *before* agent creation — because the **Bot Service** is created up front and requires the blueprint's client ID to already exist.
- **Bot Service** — `msaAppId` is the Agent Blueprint client ID; its `endpoint` is the deterministic agent endpoint URL you will create later (`https://${accountName}.services.ai.azure.com/api/projects/${projectName}/agents/${agentName}/endpoint/protocols/activityProtocol?api-version=2025-05-15-preview`). A **Microsoft Teams channel** is then connected to the Bot Service.
- **Azure Storage account + two tables** (`digitalworkerallowlist`, `workitems`) for allowlist and work-item persistence.
- **Monitoring resources** (Log Analytics + Application Insights + project AppInsights connection) — only when `ENABLE_MONITORING=true` (default). See [Monitoring & Observability](#-monitoring--observability).

Role assignments created by `/infra` (all granted to **managed identities**, never to your user):

| Role | Granted to | Scope |
|------|-----------|-------|
| **AcrPull** | Foundry project system MI | Container Registry |
| **Cognitive Services User** | Foundry project system MI | Foundry account |
| **Contributor** | Deployment-script UMI | Resource group |
| **Cognitive Services User** | Deployment-script UMI | Resource group |
| **Log Analytics Reader** *(if monitoring enabled)* | Foundry project MI | Application Insights |

> `/infra` does **not** grant **you** any roles (e.g., it does not give you ACR Contributor). Your ability to run the post-provision scripts comes from your own subscription/tenant roles — see the prerequisites and Phase 2 below.

### Phase 2 — the `postprovision` hook runs the scripts

The scripts below run under **your `az` / `azd` login**, so the permissions listed are what **you (the person running `azd provision`)** must hold. Permissions fall into three buckets: **Azure RBAC** (control plane), **Foundry data-plane** (token for `https://ai.azure.com`), and **Entra directory roles / Microsoft Graph** (token for `https://graph.microsoft.com`).

| Script | What it does | Permissions required to run |
|--------|--------------|-----------------------------|
| `post-provision.ps1` | Orchestrator — runs the scripts below in order; gates the one-time steps behind the `DIGITAL_WORKER_SETUP_DONE` azd env marker. | None of its own (inherits the requirements below). |
| `build-docker-image-acr.ps1` | Publishes the .NET app and builds + pushes the container image using **ACR Build** (cloud build). | **Contributor** on the Azure Container Registry (ACR Build queues a task: `Microsoft.ContainerRegistry/registries/scheduleRun/action`). |
| `agent-creation-script.ps1` | Creates the Foundry **hosted agent version** (referencing the blueprint), polls until `active`, grants the agent's default instance identity **Cognitive Services User** (+ **Storage Table Data Contributor**), and **patches the endpoint** for activity protocol + `BotServiceRbac` auth. | **Owner** or **User Access Administrator** on the resource group (for `roleAssignments/write`) **+ Azure AI User** (or Cognitive Services User) on the Foundry project (to create the agent version). |
| `publish-digital-worker.ps1` | Calls Foundry's `microsoft365/publish` API to publish the agent as an **AI Teammate** (validates properties, builds the manifest, submits to the MOS3 catalog). The agent then appears in the **Requests** tab in MAC. | **Azure AI User** (or equivalent publish-capable role) on the Foundry project **+ Frontier preview** tenant enrollment. |
| `create-blueprintsp-oauth2-grants.ps1` | Creates tenant-wide (`AllPrincipals`) **OAuth2 permission grants** on the blueprint SP (Prod MCP, APEX, Microsoft Graph reaction scopes), then calls `add-blueprint-inheritable-scopes.ps1`. | **Cloud Application Administrator** (for `AllPrincipals` admin consent — or `DelegatedPermissionGrant.ReadWrite.All` / `Directory.ReadWrite.All`). |
| `add-blueprint-inheritable-scopes.ps1` | Sets/merges **inheritablePermissions** (Graph reaction scopes) on the `agentIdentityBlueprint` app so each agent instance inherits them. Called by the OAuth2 grants script. | **Blueprint owner** (Agent ID Developer) **or Agent ID Administrator**. |
| `add-current-user-as-blueprint-owner.ps1` | Adds the deploying user as an **Owner** of the blueprint application (temporary fix so the OAuth2/inheritable steps work). Non-blocking — warns and continues if it lacks privileges. | **Cloud Application Administrator / Application Administrator** (`Application.ReadWrite.All`) to add the first owner, **or** already be an owner. |
| `build-docker-image.ps1` | **Not run by the hook** — a local `docker build` + push variant (superseded by the ACR Build script). Only relevant if invoked manually. | **AcrPush** on the registry (for `az acr login` + `docker push`). |

---

## 📊 Monitoring & Observability

`azd provision` can deploy a Log Analytics workspace + Application Insights and wire them to the Foundry project so the agent emits traces, logs, and metrics. This is controlled by a single boolean flag.

### Enable / disable

Monitoring is **on by default**. Toggle it via the `ENABLE_MONITORING` azd environment variable:

```powershell
azd env set ENABLE_MONITORING false   # do not create monitoring resources, and do not use monitoring
azd env set ENABLE_MONITORING true    # default: create Log Analytics + Application Insights
azd provision
```

When enabled, provisioning creates:

- A **Log Analytics workspace** and an **Application Insights** instance.
- An **`AppInsights` connection** on the Foundry project — this is what causes the platform to auto-inject `APPLICATIONINSIGHTS_CONNECTION_STRING` into the agent container at runtime (no Docker build-arg needed).
- A **`Log Analytics Reader`** role assignment for the Foundry project's managed identity, required for running **evaluations** over agent-generated traces.

When disabled, none of the above is created, no connection string is injected, and the agent runs normally with telemetry simply not sent.

### Per-version / per-instance telemetry

In the autopilot (digital worker) model, one blueprint spawns many agent instances, and updating endpoint traffic routing leaves **multiple agent versions active at once** (existing sessions stay on the previous version until they go idle). To make this debuggable, every telemetry item is stamped with the Foundry-injected identifiers via `FoundryInstanceTelemetryInitializer`:

| Foundry env var | Mapped to |
|-----------------|-----------|
| `FOUNDRY_AGENT_NAME` | `cloud_RoleName` + `agentName` dimension |
| `FOUNDRY_AGENT_VERSION` | `application_Version` + `agentVersion` dimension |
| `FOUNDRY_AGENT_DEFAULT_INSTANCE_CLIENT_ID` | `cloud_RoleInstance` + `agentInstanceClientId` dimension |
| `FOUNDRY_AGENT_SESSION_ID` | `foundrySessionId` dimension |

Example queries (App Insights → Logs):

```kusto
// See which agent versions are serving traffic over time (spot draining sessions on an old version)
requests
| summarize count() by application_Version, bin(timestamp, 5m)

// Drill into a single version's requests for debugging
requests
| where application_Version == "<suspect-version>"
| project timestamp, application_Version, cloud_RoleInstance, tostring(customDimensions.foundrySessionId), resultCode, duration

// Compare error rate across versions during a rollout
requests
| summarize total = count(), failed = countif(success == false) by application_Version
| extend failureRate = todouble(failed) / total
```

---

## 📖 Additional Resources

- [Foundry Container Agents Documentation](https://github.com/microsoft/container_agents_docs)
- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Agent Blueprint Configuration](https://dev.teams.microsoft.com/tools/agent-blueprint)

---

## 🤝 Support

For issues or questions, please refer to the official documentation or contact your Azure administrator.

