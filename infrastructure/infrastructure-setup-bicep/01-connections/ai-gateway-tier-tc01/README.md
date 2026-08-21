---
description: Simplest TC01 happy path for the Azure API Management AI Gateway tier (preview). Bicep creates a Foundry account, project, and gpt-5.4 model; you create the gateway and import the model from the ai.gateway.azure.com portal, add a token rate-limit policy, and test with the included script. Key-based, no user-assigned identity.
page_type: sample
products:
- azure
- azure-resource-manager
- azure-api-management
urlFragment: ai-gateway-tier-tc01
languages:
- bicep
- json
- python
---

# AI Gateway tier (preview) — TC01: consume a model via the AI Gateway (Bicep + script)

This is the **simplest** TC01 happy path for the **AI Gateway tier (preview)** of Azure
API Management — the dedicated AI gateway you manage at
[`ai.gateway.azure.com`](https://ai.gateway.azure.com). It uses **Bicep for the Foundry
model, the portal for the gateway, and a script to test** — with **no user-assigned
identity** (key-based backend auth).

> [!IMPORTANT]
> The AI Gateway tier is in **public preview**, available only in **East US 2** and
> **Sweden Central**. The gateway, model import, and policies are managed from the
> standalone portal (or the preview management API `2026-05-01-preview`) — they are
> **not** in the published Bicep/ARM reference yet. So this sample is a **hybrid**:
> Bicep provisions the model the gateway imports; the gateway itself is created in the
> portal. This is the verified, correct path today.

## How this maps to TC01

| TC01 step | Where | What you do |
|-----------|-------|-------------|
| 1. Create project | **Bicep** | `main.bicep` creates the account + project. |
| 2. Create model | **Bicep** | `main.bicep` deploys the `gpt-5.4` model. |
| 3. Create gateway + connect model | **Portal** | Create the gateway at `ai.gateway.azure.com`, then **Import from Foundry** (key-based). |
| 4. Add a policy | **Portal** | Add a **Token rate limit** policy on the model. |
| 5. Test via "Discover" | **Script** | Run [`samples/test-model-via-gateway.py`](./samples/test-model-via-gateway.py), or use the portal **Discover** playground. |

## Why no UAMI?

The AI Gateway tier **Import from Foundry** wizard defaults to **key-based** backend
authentication: the gateway reads the account's API key at import time and sends it in
the `api-key` header to the backend. No managed identity is involved. That is why
`main.bicep` keeps **local auth enabled** (`disableLocalAuth: false`) — key-based import
needs it. (Managed identity is the alternative, but you asked for no UAMI.)

## Prerequisites

1. **Azure CLI** logged in (`az login`) to the target subscription.
2. Quota for `gpt-5.4` (`GlobalStandard`) in **East US 2** or **Sweden Central**.
3. Access to the **AI Gateway tier** preview and permission to sign in at
   `ai.gateway.azure.com` (Microsoft Entra ID).
4. For key-based import: **Reader** on the subscription/account plus permission to list
   the account keys (for example, **Cognitive Services Contributor**).
5. `pip install openai` for the test script.

---

## Steps 1–2 — deploy the Foundry model (Bicep)

```bash
az login
az account set --subscription <subscription-id>
az group create --name <your-rg> --location eastus2

az deployment group create \
  --resource-group <your-rg> \
  --template-file main.bicep \
  --parameters @samples/parameters.json
```

Note the outputs — you'll pick this account in the import wizard:

```bash
az deployment group show -g <your-rg> -n main \
  --query "properties.outputs.{account:accountName.value, model:modelName.value, region:location.value}" -o jsonc
```

## Step 3 — create the gateway and import the model (portal)

Verified from the [AI Gateway tier quickstart](https://learn.microsoft.com/azure/api-management/quickstart-ai-gateway-create)
and [setup wizard](https://learn.microsoft.com/azure/api-management/ai-gateway-setup).

**Create the gateway:**

1. Go to [`ai.gateway.azure.com`](https://ai.gateway.azure.com) and sign in with Microsoft Entra ID.
2. Select **Create gateway**.
3. Enter a **Name** — it becomes the endpoint `https://<gateway>.azure-api.net`.
4. Select your **Subscription** and a preview region (**East US 2** or **Sweden Central**).
5. Select **Create**. Activation typically takes under a minute.

**Import the model from Foundry (key-based):**

1. On **Home → Configure your gateway**, select **Get started** (or open **Models → Add models**).
2. Select **Import from Foundry**.
3. Select the **subscription** and the **Foundry account** from the Bicep outputs. The wizard
   lists its deployments (including `gpt-5.4`).
4. For **backend authentication**, keep **Key-based** (default) — no UAMI.
5. Select **Import**. The wizard verifies requirements and registers `gpt-5.4` as a model.

## Step 4 — add a token rate-limit policy (portal)

Verified from [Govern and secure assets](https://learn.microsoft.com/azure/api-management/ai-gateway-govern-secure-assets).

1. Open **Policies → Add policy**.
2. On **Type**, choose **Token rate limit**.
3. On **Assets**, select the `gpt-5.4` model.
4. On **Configure**, set the token allowance (per **minute**, **hour**, or **day**) and the
   dimension to count against (**caller identity** or **caller IP address**). Select **Create**.

Requests over the limit return **HTTP 429** (with `Retry-After`).

## Step 5 — test the model through the gateway ("Discover")

Get a key and the base URL from the gateway:

1. On the **Keys** page, copy the **built-in key** (the one the Discover playground uses) or
   select **Create API key** for a runtime access key.
2. Copy the base URL from the gateway **overview** page:
   `https://<gateway>.azure-api.net/default/models/openai/v1`.

Then run the script (the code equivalent of the portal **Discover** playground):

```bash
pip install openai

export AI_GATEWAY_BASE_URL="https://<gateway>.azure-api.net/default/models/openai/v1"
export AI_GATEWAY_API_KEY="<gateway-key>"

# single call — verifies the model answers through the gateway
python samples/test-model-via-gateway.py --prompt "Say hello in five words."

# burst — exercises the token rate-limit policy (expect HTTP 429 after the budget)
python samples/test-model-via-gateway.py --repeat 12
```

Or open the **Discover** page in the portal and select the model to invoke it in the
built-in playground.

> The gateway reads the `api-key` header, authenticates it, applies your policies
> (including the step-4 token limit), and routes to the imported `gpt-5.4` deployment.
> All OpenAI-compatible providers share the `/default/models/openai/v1` path; callers pass
> the model name in the `model` field.

## Parameters (Bicep)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `aiServicesName` | `foundry` | Base name for the account (a unique suffix is appended). |
| `projectName` | `gateway-project` | Project name. |
| `location` | `eastus2` | Region — restricted to the tier preview regions (`eastus2`, `swedencentral`). |
| `modelName` / `modelFormat` / `modelVersion` | `gpt-5.4` / `OpenAI` / `2026-03-05` | Model to deploy and import. |
| `modelSkuName` / `modelCapacity` | `GlobalStandard` / `40` | Deployment SKU and capacity. `gpt-5.4` may need more — set a value you have quota for (the reference deployment used 681). |

## References

- [AI Gateway tier overview](https://learn.microsoft.com/azure/api-management/ai-gateway-overview)
- [Quickstart: Create an AI Gateway tier instance](https://learn.microsoft.com/azure/api-management/quickstart-ai-gateway-create)
- [Get started with the setup wizard](https://learn.microsoft.com/azure/api-management/ai-gateway-setup)
- [Manage models and tools](https://learn.microsoft.com/azure/api-management/ai-gateway-manage-models-tools)
- [Govern, secure, and operate](https://learn.microsoft.com/azure/api-management/ai-gateway-govern-secure-assets)

> **Note:** `az bicep build` may surface `BCP081` warnings for the CognitiveServices
> preview API versions — expected and non-blocking.
