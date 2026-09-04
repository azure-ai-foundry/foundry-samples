# Python Autopilot agents

These samples deploy Python agents directly to Microsoft Foundry Agent Service
and publish their hosted-agent identity blueprints as Microsoft 365 Autopilots.
Each sample owns its agent code, `azure.yaml`, runtime dependencies, and
publication metadata. The deployment, publication, approval, and permission
workflow below is shared.

Run commands from the sample directory unless a command says otherwise.

## Required permissions

Installing the local tools, creating an `azd` environment, and setting its
values do not require Azure or Microsoft 365 permissions. Signing in
authenticates your account but does not grant it additional access.

Cloud operations require the following access:

- **Deploy or manage sessions:** **Foundry Project Manager** at the Foundry
  project scope. This grants the data-plane permissions to create and update
  hosted agents, manage their sessions, and create role assignments for the
  platform-created agent identity when needed.
- **Publish the Autopilot:** A Microsoft 365 license and Foundry project
  data-plane access to read the deployed agent version and submit its Microsoft
  365 publication. **Foundry Project Manager** covers the hosted-agent
  deployment workflow.
- **Approve and activate the blueprint:** **AI Administrator** or **Global
  Administrator** in the Microsoft 365 admin center, plus a Microsoft 365
  license.
- **Create or use an instance in Teams:** A Microsoft 365 license, an approved
  blueprint, and access allowed by the tenant's app policies.
- **Enable Agent 365 telemetry:** Native publication applies the configured
  default permissions to the managed agent identity blueprint. A tenant
  administrator may still need to grant consent for
  `Agent365.Observability.OtelWrite` using the organization's standard
  admin-consent process.

If a sample provisions Azure resources or an Azure Bot Service resource, it can
require broader permissions than this direct-code-deployment workflow. Follow
that sample's README. In particular, provisioning role assignments generally
requires **Owner**, and creating or configuring Azure Bot Service requires
**Owner** or **Contributor** at the resource-group scope. Foundry project roles
do not include `Microsoft.BotService/*`.

## Prerequisites

1. An existing Microsoft Foundry project and model deployment in a
   [supported hosted-agent region](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents#region-availability).
2. A tenant with Microsoft Agent 365 and qualifying Microsoft 365 licensing.
3. [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
   **1.31.2 or later**.
4. [Azure CLI (`az`)](https://learn.microsoft.com/cli/azure/install-azure-cli).
5. Python 3.11 or later.

Install or update the Foundry agent extension:

```powershell
azd ext install azure.ai.agents
# If it is already installed:
azd ext upgrade azure.ai.agents
```

## Sign in

Use the same tenant for Azure CLI and Azure Developer CLI:

```powershell
az login --tenant <tenant-id>
azd auth login --tenant-id <tenant-id>
```

## Deploy

Configure the `azd` environment values documented by the sample, then deploy
from the sample directory:

```powershell
azd deploy
```

Direct code deployment packages the sample and creates a new hosted-agent
version. It does not publish or update the Microsoft 365 app.

Existing sessions can continue on their current sandbox after deployment. Run
the shared session-stop script so each session resumes against the latest active
version on its next invocation:

```powershell
..\scripts\stop-agent-sessions.ps1 -AgentName <agent-name>
```

Stopping a session preserves its logical session and persisted filesystem state.
Do not delete sessions or republish the Microsoft 365 app for a code-only
deployment. Pass `-Environment <environment-name>` when the active environment
is not the intended target.

## Publish to Microsoft 365

Publication is normally needed only for the initial Microsoft 365 app or when
its manifest, metadata, or other publication-owned configuration changes.
Publication metadata is declared in the sample's `azure.yaml`. From the sample
directory, run:

```powershell
azd ai agent publish
```

The command reads the tenant-scoped Autopilot configuration from
`activity.publish` in `azure.yaml`. To change publication metadata, edit that
block and run the command again. For a one-time override, the command supports
`--display-name` and `--app-version`.

## Approve and create an instance

After publication:

1. An **AI Administrator** or **Global Administrator** opens
   [Agents in the Microsoft 365 admin center](https://admin.cloud.microsoft/?#/agents/all/requested),
   approves the pending blueprint, and verifies it appears in the Agent 365
   registry.
2. A licensed user opens **Apps** > **Agents for your team** in Teams, selects
   the approved blueprint, and creates an instance.

Tenant app policies can restrict which users discover or use the approved
agent.

## Agent 365 observability permission

Azure Monitor export does not require the Agent 365 permission. Exporting
telemetry to Agent 365 requires permission
`Agent365.Observability.OtelWrite` on resource application
`9b975845-388f-4429-889e-eab1ef63949c`.

For an agentic user, `azd ai agent publish` applies the service-configured
default permission scopes to the Foundry managed agent identity blueprint and
preserves the mandatory Agent 365 trace permission. Customers should not PATCH
the blueprint as part of the normal deployment or publication workflow.

A tenant administrator may still need to grant consent to the **blueprint
application ID** for the delegated permission using the organization's standard
admin-consent process. The request must identify the blueprint application ID,
the resource application ID above, and scope
`Agent365.Observability.OtelWrite`.

The current native publish command does not expose optional permission-scope
selection. Permission updates are additive/default-oriented: omitting a
previously granted optional scope does not reliably revoke it or remove its
resource application. After consent and inheritance are confirmed, existing
agentic-user identities receive the permission the next time they mint a token
for the resource; no AU recreation or redeployment is required.

For a hosted-agent application identity instead of an agentic user, follow
[Grant Agent 365 observability permissions](https://learn.microsoft.com/azure/foundry/agents/how-to/grant-agent-365-permissions).
That app-role-assignment workflow requires **Global Administrator** or
**Application Administrator** in Microsoft Entra ID.

## References

- [Deploy a hosted agent](https://learn.microsoft.com/azure/foundry/agents/how-to/deploy-hosted-agent)
- [Publish an Autopilot in Microsoft Agent 365](https://learn.microsoft.com/azure/foundry/agents/how-to/agent-365)
- [Hosted agent permissions](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-permissions)
- [Agent 365 observability concepts](https://learn.microsoft.com/microsoft-agent-365/developer/observability-concepts)
