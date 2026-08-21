---
description: Self-contained single-project AI Gateway happy path (TC01) — public, no VNet required. Creates a Microsoft Foundry account + project, deploys a model (default gpt-5.4), fronts it with a public StandardV2 Azure API Management AI Gateway, applies an LLM token-limit policy, and adds a bring-your-own-model connection consumed by a prompt agent — all in one deployment.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: public-byom-ai-gateway-apim
languages:
- bicep
- json
- python
---

# Microsoft Foundry: AI Gateway happy path (TC01) — consume a model via APIM, end to end

This sample is the **simplest, fully self-contained** AI Gateway happy path — **public,
with no virtual network, subnet, or private endpoint required**. In **one
`az deployment group create`** it stands up everything and leaves you with a model you
can call **through** a public Azure API Management (APIM) AI Gateway:

- A **user-assigned managed identity (UAMI)** shared by the account and project.
- A **Microsoft Foundry account (AIServices) + project**, both using the UAMI.
- A **model deployment** (default `gpt-5.4`) on the account.
- A public **StandardV2 APIM AI Gateway** with a system-assigned MI, RBAC'd
  `Cognitive Services User` on the account.
- The `/inference` API with the **managed-identity + backend-rewrite** policy chain,
  **plus an LLM token-limit policy** on the chat-completions operation.
- A **BYOM connection** on the project that surfaces the model as
  `<connectionName>/<modelName>` for a prompt agent.

It is the **self-contained counterpart** of [`../public-byom-apim`](../public-byom-apim/),
which layers onto an *existing* project + backend account and adds no policy. Use this
one when you want a single command that creates and wires up the whole thing.

Reference: [Bring your own model to Foundry Agent Service](https://learn.microsoft.com/azure/foundry/agents/how-to/ai-gateway).

## How this maps to TC01

| TC01 step | What this sample does |
|-----------|-----------------------|
| 1. Create project | `account` + `project` (both user-assigned identity) in `main.bicep`. |
| 2. Create model in the project | `modelDeployment` (default `gpt-5.4`). |
| 3. Create gateway + connect it to the model | `apimService` + `apimBackendRole` + `inferenceApi` (MI policy chain, backend = this account) + `byomConnection`. |
| 4. Add a policy | `tokenLimit` module — `llm-token-limit` on the chat-completions operation. |
| 5. Test the model via the gateway | Portal **Discover**, then [`samples/consume-model.py`](./samples/consume-model.py). |

## Least-privilege RBAC for the three roles

This sample targets a hub/spoke governance split. The least-privilege Azure roles are:

| Role | Responsibility | Least-privilege role(s) | Scope |
|------|----------------|-------------------------|-------|
| **Admin** | Deploys this template: account, model, APIM, policy, role assignment. | **Owner** (or **Contributor** + **User Access Administrator**) — the role assignment in step 3 needs `Microsoft.Authorization/roleAssignments/write`. On Foundry itself, **Azure AI Account Owner**. | Resource group |
| **Developer** | Builds/updates prompt agents that reference `<connection>/<model>`. | **Azure AI User** (data-plane build/test). To publish/manage agents, **Azure AI Project Manager**. | Project |
| **Consumer** | Invokes the agent (inference only). | **Azure AI User** at project scope, or a per-agent assignment. | Project / agent |

> [!NOTE]
> Avoid the legacy **Azure AI Developer** role for Foundry projects — it is scoped to
> Azure Machine Learning / hub workspaces, not Foundry projects and agents. Use
> **Azure AI User** / **Azure AI Project Manager** instead.

## Why a user-assigned identity?

APIM's `validate-azure-ad-token` policy must pin the **application (client) ID** of the
project's identity. A **system-assigned** identity's client ID does not exist until
*after* the project is created — which would force a two-step deploy (create project →
look up the app ID → create APIM). A **user-assigned** identity's `clientId` is known at
deploy time, so the whole sample deploys in **one shot** while keeping the policy pinned
to exactly one caller. This mirrors templates
[17](../../17-private-network-standard-user-assigned-identity-agent-setup/),
[20](../../20-user-assigned-identity/), and
[32](../../32-customer-managed-keys-user-assigned-identity/).

> Prefer a **system-assigned** project identity? Use the two-step golden path instead:
> deploy [template 40](../../40-basic-agent-setup/) / [41](../../41-standard-agent-setup/),
> then [`../public-byom-apim`](../public-byom-apim/) with the project's MI app ID.

## Prerequisites

1. **Azure CLI** installed and logged in (`az login`) to the target subscription.
2. Quota for the model (default `gpt-5.4` `2026-03-05`, `GlobalStandard`) in your chosen `location`.
   Confirm the model/version is available in your region with
   `az cognitiveservices account list-models` if you change `location`.
3. Rights to create a **role assignment** in the resource group (Owner, or
   Contributor + User Access Administrator).
4. **No virtual network** — this sample is fully public; nothing here needs a VNet.

## How to deploy

```bash
az login
az account set --subscription <subscription-id>

az group create --name <your-rg> --location eastus

# Edit samples/parameters.json (publisherEmail / publisherName at minimum), then:
az deployment group create \
  --resource-group <your-rg> \
  --template-file main.bicep \
  --parameters @samples/parameters.json
```

APIM (StandardV2) takes ~30–45 minutes to provision on first deployment.

Grab the outputs you'll need to consume the model:

```bash
az deployment group show -g <your-rg> -n main \
  --query "properties.outputs.{endpoint:projectEndpoint.value, model:modelReference.value, apim:apimGatewayUrl.value}" -o jsonc
```

## Step 5 — test the model through the gateway

### Option A — Portal "Discover"

1. Open the [Foundry portal](https://ai.azure.com/) and select the project.
2. Go to **Management center → Connected resources** (or **Models + endpoints**) and
   open the `ai-gateway` connection.
3. Use **Discover** to list/validate the model served through the gateway. Success
   means the connection reaches the model **via APIM**.

### Option B — Consume from code (proves inference + token limit)

```bash
pip install "azure-ai-projects>=2.0.0" azure-identity

# single call
python samples/consume-model.py \
  --endpoint <projectEndpoint output> \
  --model    <modelReference output> \
  --prompt   "Say hello in five words."

# burst to exercise the token-limit policy (expect HTTP 429 after the budget)
python samples/consume-model.py \
  --endpoint <projectEndpoint output> \
  --model    <modelReference output> \
  --repeat   8
```

> [!IMPORTANT]
> A BYOM model works **only** with a *prompt agent* invoked through the **Responses API**.
> The classic Assistants API (`create_agent` + threads + runs) cannot resolve
> `<connection>/<model>` and fails with `Failed to resolve model info`. A direct
> user-issued call to the APIM endpoint also fails by design — `validate-azure-ad-token`
> only accepts the project's managed identity. The prompt agent runs server-side *as*
> the project identity, which is the token APIM expects.

## The token-limit policy (step 4)

`modules/apim-token-limit-policy.bicep` attaches `llm-token-limit` to the
chat-completions operation. Tune it with `tokensPerMinute` (default `1000`) and
`estimatePromptTokens` (default `true`). The gateway meters **real model token usage**
(prompt + completion) and returns **HTTP 429** once the per-minute budget is exhausted.

Token-based limiting shows the AI Gateway capability better than a raw request count: it
governs spend and throughput by actual model consumption. With `estimatePromptTokens =
true`, APIM estimates the prompt size and can block **before** hitting the backend; set
it `false` to meter only the usage the model reports.

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `aiServicesName` | No | `foundry` | Base name for the account (a unique suffix is appended). |
| `projectName` | No | `gateway-project` | Project name. |
| `location` | No | `eastus` | Region for account, project, model, APIM. |
| `modelName` / `modelFormat` / `modelVersion` | No | `gpt-5.4` / `OpenAI` / `2026-03-05` | Model to deploy and surface. Ensure the version is available in your region. |
| `modelSkuName` / `modelCapacity` | No | `GlobalStandard` / `40` | Deployment SKU and TPM. |
| `apimName` | No | auto | Globally unique APIM name. Auto-generated if empty. |
| `publisherEmail` | **Yes** | — | Publisher email required by APIM. |
| `publisherName` | **Yes** | — | Publisher organization required by APIM. |
| `connectionName` | No | `ai-gateway` | Foundry connection name; surfaces as `<connectionName>/<modelName>`. |
| `inferenceApiVersion` | No | `2024-10-21` | Inference API version sent to the backend. |
| `tokensPerMinute` | No | `1000` | Tokens-per-minute budget enforced by the gateway. |
| `estimatePromptTokens` | No | `true` | Estimate prompt tokens before the backend call (`true`) or meter only actual usage (`false`). |

## Outputs

`accountName`, `projectName`, `projectResourceId`, `projectEndpoint`, `apimName`,
`apimGatewayUrl`, `connectionName`, `modelReference` (`<connection>/<model>`),
`userAssignedIdentityClientId`.

> **Note:** `az bicep build` may surface `BCP081` warnings for the CognitiveServices
> preview API versions — expected and non-blocking.
