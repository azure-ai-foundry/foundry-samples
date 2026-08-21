---
description: Keyless TC01 happy path for the Azure API Management AI Gateway tier (preview). Bicep creates a Foundry account, project, and gpt-5.4 model with local auth disabled; you create the gateway and import the model with Managed identity backend auth (the gateway's system-assigned identity + Foundry User role), add a token rate-limit policy, and test with the included script. No user-assigned identity, no stored Foundry key.
page_type: sample
products:
- azure
- azure-resource-manager
- azure-api-management
urlFragment: ai-gateway-tier-tc01-mi
languages:
- bicep
- json
- python
---

# AI Gateway tier (preview) — TC01 (Bicep + script)

This is the AI Gateway tier TC01 happy path.

> [!IMPORTANT]
> The AI Gateway tier is in **public preview**, available only in **East US 2** and
> **Sweden Central**. The gateway, model import, and policies are managed from the
> standalone portal ([`ai.gateway.azure.com`](https://ai.gateway.azure.com)) — not the
> published Bicep/ARM reference. So this is a **hybrid**: Bicep provisions the Foundry
> model; the gateway is created in the portal.

## How this maps to TC01

| TC01 step | Where | What you do |
|-----------|-------|-------------|
| 1. Create project | **Bicep** | `main.bicep` creates the account + project. |
| 2. Create model | **Bicep** | `main.bicep` deploys the `gpt-5.4` model (local auth disabled). |
| 3. Create gateway + connect model | **Portal** | Create the gateway, then **Import from Foundry** with **Managed identity**. |
| 4. Add a policy | **Portal** | Add a **Token rate limit** policy on the model. |
| 5. Test via "Discover" | **Script** | Run [`samples/test-model-via-gateway.py`](./samples/test-model-via-gateway.py). |

## Prerequisites

1. **Azure CLI** logged in (`az login`).
2. Quota for `gpt-5.4` (`GlobalStandard`) in **East US 2** or **Sweden Central**.
3. Access to the **AI Gateway tier** preview (`ai.gateway.azure.com`, Microsoft Entra ID).
4. To let the import wizard grant the role for you: **User Access Administrator** or
   **Owner** on the account. Otherwise assign the role manually (see step 3).
5. `pip install openai` for the test script.

---

## Steps 1–2 — deploy the Foundry model (Bicep)

```powershell
az login
az account set --subscription <subscription-id>
az group create --name <your-rg> --location eastus2

az deployment group create \
  --resource-group <your-rg> \
  --template-file main.bicep \
  --parameters @samples/parameters.json
```

Capture the "accountId" which you'll need later as $BACKEND_RESOURCE_ID:

```powershell
az deployment group show -g <your-rg> -n main \
  --query "properties.outputs.{account:accountName.value, accountId:accountId.value, model:modelName.value}" -o jsonc
```

## Step 3 — create the gateway and import the model with managed identity (portal)

Verified from the [quickstart](https://learn.microsoft.com/azure/api-management/quickstart-ai-gateway-create)
and [Manage models and tools](https://learn.microsoft.com/azure/api-management/ai-gateway-manage-models-tools).

**Create the gateway:**

1. Go to [`ai.gateway.azure.com`](https://ai.gateway.azure.com) and sign in with Microsoft Entra ID.
2. Select **Create gateway**, enter a **Name** (becomes `https://<gateway>.azure-api.net`),
   choose your **Subscription** and a preview region (**East US 2** or **Sweden Central**), and **Create**.

**Import the model with managed identity:**

1. Open **Models → Add models → Import from Foundry**.
2. Select the **subscription** and the **Foundry account** from the Bicep outputs.
3. For **backend authentication**, choose **Managed identity**.
4. Select **Create/Import**. The wizard enables the gateway's **system-assigned identity**
   (if it doesn't have one) and grants it the **Foundry User** role on the account, then
   registers `gpt-5.4` as a model.

If you don't have permission for the wizard to assign the role, an administrator grants
the gateway's system-assigned identity the **Foundry User** role on the account. Gather
the two values first, then assign the role (reference it by its stable GUID — the Foundry
roles were recently renamed). Requires **Azure CLI 2.57.0+**.

**a. Get the gateway's managed-identity principal ID (from the portal).** In the AI Gateway
tier portal, open the **Managed identities** page → **Configure identities**, turn on the
**System-assigned identity**, and copy its **Object (principal) ID**. The tier is
portal-managed, so this ID comes from the portal, not the Azure CLI.

```powershell
$env:GATEWAY_PRINCIPAL_ID="<object-principal-id-copied-from-the-portal>"
```

**b. Get the Foundry account resource ID** — the `accountId` output from steps 1–2:

```powershell
# Please skip this step if you have already assigned $BACKEND_RESOURCE_ID from "accountId" in steps 1–2.
$env:BACKEND_RESOURCE_ID=$(az deployment group show -g <your-rg> -n main \
  --query properties.outputs.accountId.value -o tsv)
```

**c. Assign the Foundry User role** (`53ca6127-db72-4b80-b1b0-d745d6d5456d` = **Foundry User**, formerly Azure AI User):

```powershell
az role assignment create \
  --assignee-object-id "$GATEWAY_PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "53ca6127-db72-4b80-b1b0-d745d6d5456d" \
  --scope "$BACKEND_RESOURCE_ID"
```

**d. Verify** (role assignments can take a few minutes to propagate):

```powershell
az role assignment list \
  --assignee-object-id "$GATEWAY_PRINCIPAL_ID" \
  --scope "$BACKEND_RESOURCE_ID" \
  --output table
```

## Step 4 — add a token rate-limit policy (portal)

Verified from [Govern and secure assets](https://learn.microsoft.com/azure/api-management/ai-gateway-govern-secure-assets).

1. Open **Policies → Add policy**.
2. On **Type**, choose **Token rate limit**.
3. On **Assets**, select the `gpt-5.4` model.
4. On **Configure**, set the token allowance (per **minute**, **hour**, or **day**) and the
   dimension (**caller identity** or **caller IP**). Choose 100 tokens per minute limit per caller identity. Select **Create**. Over-limit → **HTTP 429**.

## Step 5 — test the model through the gateway ("Discover")

Get a key and the base URL from the gateway (**Keys** page + **Home** page), then:

```powershell
pip install openai

$env:AI_GATEWAY_BASE_URL="https://<gateway>.azure-api.net/default/models/openai/v1"
$env:$AI_GATEWAY_API_KEY="<gateway-key>"

# single call — verifies the model answers through the gateway (keyless backend)
python samples/test-model-via-gateway.py --prompt "What is the meaning of life?" --repeat 16
```

You would see the following message.
```powershell
...
[14/16] 429 THROTTLED by the gateway token rate-limit policy.
[15/16] 429 THROTTLED by the gateway token rate-limit policy.
[16/16] 429 THROTTLED by the gateway token rate-limit policy.

Done. 14 of 16 calls were throttled by the token rate-limit policy.
```

Or open the **Discover** page in the portal and select the model to invoke it in the
built-in playground.

## Parameters (Bicep)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `aiServicesName` | `foundry` | Base name for the account (a unique suffix is appended). |
| `projectName` | `gateway-project` | Project name. |
| `location` | `eastus2` | Region — restricted to the tier preview regions (`eastus2`, `swedencentral`). |
| `modelName` / `modelFormat` / `modelVersion` | `gpt-5.4` / `OpenAI` / `2026-03-05` | Model to deploy and import. |
| `modelSkuName` / `modelCapacity` | `GlobalStandard` / `40` | Deployment SKU and capacity. `gpt-5.4` may need more — set a value you have quota for (the reference deployment used 681). |

## References

- [Manage models and tools](https://learn.microsoft.com/azure/api-management/ai-gateway-manage-models-tools) (managed-identity import)
- [Quickstart: Create an AI Gateway tier instance](https://learn.microsoft.com/azure/api-management/quickstart-ai-gateway-create)
- [Govern, secure, and operate](https://learn.microsoft.com/azure/api-management/ai-gateway-govern-secure-assets)
- [Role-based access control for Microsoft Foundry](https://learn.microsoft.com/azure/foundry/concepts/rbac-foundry) (Foundry User role)
