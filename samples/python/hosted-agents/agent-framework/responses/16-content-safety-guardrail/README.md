# Content safety guardrail (Responses protocol)

An [Agent Framework](https://github.com/microsoft/agent-framework) agent hosted on Microsoft Foundry using the **Responses protocol**, with a Responsible AI (RAI) **content safety guardrail** attached. The guardrail screens the prompts the agent receives and the responses it returns against an RAI policy, so harmful content is filtered according to your safety configuration.

## How it works

The agent itself is the basic `FoundryChatClient` agent served via `ResponsesHostServer` — see [main.py](src/agent-framework-content-safety-guardrail/main.py). The guardrail is **not** code; it's a definition-level setting. The agent declares a `policies` list with a `rai_policy` entry that points to an RAI policy by its full Azure Resource Manager (ARM) resource ID:

```yaml
policies:
  - type: rai_policy
    rai_policy_name: /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account>/raiPolicies/<policy-name>
```

The platform applies that policy to the agent at runtime. When you omit the `policies` block, the agent deploys without a content safety guardrail. When you include the `policies` block but omit `rai_policy_name`, the platform applies the default policy, `Microsoft.DefaultV2`. For a conceptual overview, see [Add a content safety guardrail to a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/add-hosted-agent-guardrails).

## Prerequisites

1. An RAI policy created on your Foundry resource, and its full ARM resource ID. To create one, see [Configure guardrails and controls](https://learn.microsoft.com/en-us/azure/foundry/guardrails/how-to-create-guardrails). The ARM resource ID has this form:

   ```text
   /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account>/raiPolicies/<policy-name>
   ```

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd), then install the AI agent extension and authenticate:

   ```bash
   azd ext install azure.ai.agents
   azd auth login
   ```

## Configure the guardrail

Set `rai_policy_name` to your RAI policy's full ARM resource ID in [azure.yaml](azure.yaml). Use the full ARM resource ID, not the bare policy name.

## Option 1: Azure Developer CLI (`azd`)

### Initialize the agent project

No cloning required. Create a new folder and initialize from the manifest:

```bash
mkdir my-guardrail-agent && cd my-guardrail-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/16-content-safety-guardrail/azure.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` guides you through creating one.

> [!NOTE]
> After init, confirm that `rai_policy_name` in the generated `azure.yaml` holds your policy's full ARM resource ID.

### Provision Azure resources (if needed)

If you don't already have a Foundry project and model deployment:

```bash
azd provision
```

> [!IMPORTANT]
> If you provisioned a new Foundry project, it doesn't have your RAI policy yet. Before you deploy, [create an RAI policy](https://learn.microsoft.com/en-us/azure/foundry/guardrails/how-to-create-guardrails) on the provisioned account, then set `rai_policy_name` in the generated `azure.yaml` to that policy's full ARM resource ID. Deploying with a placeholder or nonexistent policy ID fails.

### Deploy to Foundry

```bash
azd deploy
```

The platform applies the guardrail when it creates the agent version. For the full deployment guide, see [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

### Invoke the deployed agent

```bash
azd ai agent invoke "Write a short friendly hello message."
```

## Option 2: VS Code (Foundry Toolkit)

1. Install the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.azure-ai-foundry)** extension and sign in to Azure.
1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Create Hosted Agent**, then select this sample from the gallery. The extension scaffolds the project and generates `agent.yaml`.
1. Set `rai_policy_name` in the generated `azure.yaml` to your policy's full ARM resource ID.
1. Run **Foundry Toolkit: Deploy Hosted Agent** and follow the wizard to deploy.

## Verify the guardrail

After deployment, confirm the guardrail filters content by sending a benign prompt and a prompt that violates your policy to the agent's Responses endpoint. The platform screens prompts at the input stage and rejects a violating prompt before the agent runs.

A prompt that passes the policy returns `HTTP 200` with the agent's response. A blocked prompt returns `HTTP 400` with a `content_filter` error:

```json
{
  "error": {
    "code": "content_filter",
    "message": "The request was blocked due to content safety policy violation at input stage.",
    "type": "content_safety_error"
  }
}
```

If a violating prompt isn't blocked, confirm that the policy referenced by `rai_policy_name` is configured to filter the relevant content category and severity.

## Next steps

- [Add a content safety guardrail to a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/add-hosted-agent-guardrails) — set a guardrail with `azd`, the Python SDK, or REST
- [Guardrails and controls overview](https://learn.microsoft.com/en-us/azure/foundry/guardrails/guardrails-overview) — what guardrails are and the risks they detect
- [Basic hosted agent](../01-basic/) — the agent this sample builds on
- [Manage hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/manage-hosted-agent) — monitor and manage deployed agents
