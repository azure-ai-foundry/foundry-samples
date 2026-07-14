# BYO Model via APIM AI Gateway - Testing Guide

This guide covers testing Azure AI Foundry agents that consume a **bring-your-own-model (BYOM)** deployment through a customer-owned **Azure API Management (APIM) AI Gateway**, as deployed by template 16 and its [`extensions/byom-cross-region`](../extensions/byom-cross-region/) template.

The agent never talks to the model account directly. It resolves a **BYOM model connection** (`<connection>/<deployment>`) that points at APIM, and APIM forwards the request — over a **cross-region private endpoint**, using its **managed identity** — to a backend Foundry account in a second region.

> **Private Foundry (default):** The Foundry account has `publicNetworkAccess = Disabled`. You need a secure path into the VNet (VPN Gateway, ExpressRoute, or Azure Bastion) to run the SDK tests. See [Connecting to a Private Foundry Resource](#connecting-to-a-private-foundry-resource). For easier local testing you can [switch the Foundry account to public access](#switching-the-foundry-resource-to-public-access) — backend resources (AI Search, Storage, Cosmos, and the cross-region model account) stay private regardless.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Permissions required](#permissions-required)
3. [Connecting to a Private Foundry Resource](#connecting-to-a-private-foundry-resource)
4. [Switching the Foundry Resource to Public Access](#switching-the-foundry-resource-to-public-access)
5. [Step 1: Deploy the Template](#step-1-deploy-the-template)
6. [Step 2: Capture Deployment Outputs](#step-2-capture-deployment-outputs)
7. [Step 3: Verify APIM and Private Endpoints](#step-3-verify-apim-and-private-endpoints)
8. [Step 4: Create and Verify the BYOM (APIM) Connection](#step-4-create-and-verify-the-byom-apim-connection)
9. [Step 5: Run the SDK Test](#step-5-run-the-sdk-test)
10. [Troubleshooting](#troubleshooting)
11. [Test Results Summary](#test-results-summary)

---

## Prerequisites

- Azure CLI installed and authenticated (`az login`)
- Permissions to create resources **and role assignments** — see [Permissions required](#permissions-required) below
- Python 3.10+ with the Agents v2 SDK:

  ```bash
  pip install azure-ai-projects azure-identity openai
  ```

- The BYOM extension deployed (`extensions/byom-cross-region`), which stands up APIM, the backend Foundry account in a second region, the cross-region private endpoint, and the BYOM connection.

### Permissions required

This deployment **creates role assignments** (`Microsoft.Authorization/roleAssignments/write`): the agent managed identity gets roles on AI Search / Storage / Cosmos DB, and APIM's managed identity gets a role on the backend Foundry account. Plain **Contributor is not enough** — you need one of:

- **Owner**, or
- **Contributor + User Access Administrator**

scoped to the target resource group (or the subscription).

> **Symptom if missing:** infrastructure (APIM, accounts, private endpoints, DNS) provisions fine, then the deployment fails late with:
> ```
> Authorization failed ... does not have permission to perform action
> 'Microsoft.Authorization/roleAssignments/write'
> ```

Grant **User Access Administrator** at the RG scope (least privilege), or activate it via **PIM** (Azure portal → *Privileged Identity Management* → *My roles*) on nonprod subscriptions:

```bash
az role assignment create \
  --assignee <your-object-id> \
  --role "User Access Administrator" \
  --scope "/subscriptions/<sub-id>/resourceGroups/<rg>"
```

---

## Connecting to a Private Foundry Resource

When the Foundry account has public network access **disabled** (the default), you must reach the Foundry endpoint from inside the VNet before SDK tests will work.

| Method | Use Case |
|--------|----------|
| **Azure VPN Gateway** | Connect from your local machine over an encrypted tunnel |
| **Azure ExpressRoute** | Private, dedicated connection from on-premises |
| **Azure Bastion** | Access a jump box VM on the VNet through the Azure portal |

For setup steps, see [Securely connect to Azure AI Foundry](https://learn.microsoft.com/azure/ai-foundry/how-to/configure-private-link#securely-connect-to-foundry). Once connected to the VNet, every command below works as documented.

---

## Switching the Foundry Resource to Public Access

For easier testing (no VPN/ExpressRoute/Bastion required), you can enable public network access on the **Foundry account only**. The agent still runs inside the VNet (via `networkInjections`) and reaches the private backends — AI Search, Storage, Cosmos DB, and the cross-region model account — through the Data Proxy, so those stay private. Only the Foundry control/data-plane endpoint becomes reachable from the internet.

In [`modules-network-secured/ai-account-identity.bicep`](../modules-network-secured/ai-account-identity.bicep), change the account's network settings:

```bicep
// Change from (private, default):
    networkAcls: {
      defaultAction: 'Deny'
      ...
    }
    publicNetworkAccess: 'Disabled'

// To (public for testing):
    networkAcls: {
      defaultAction: 'Allow'
      ...
    }
    publicNetworkAccess: 'Enabled'
```

Then redeploy the template. To revert, set `publicNetworkAccess: 'Disabled'` and `defaultAction: 'Deny'` and redeploy. Backend resources remain on private endpoints in both modes.

> The default stays **private** on purpose — this is a private-network template. Enable public access only while testing, then revert.

---

## Step 1: Deploy the Template

The extension deploys template 16 end-to-end plus the APIM AI Gateway, the backend Foundry account, the cross-region private endpoint, and the BYOM connection. The template creates a new APIM StandardV2 instance as part of the deployment.

APIM's `validate-azure-ad-token` policy needs the project managed-identity **app (client) ID**, which is created by this deployment. So you deploy once with a placeholder ID, then set the real value afterward (see [Set the real project identity](#set-the-real-project-identity) below).

```bash
RESOURCE_GROUP="<your-rg>"
PROJECT_REGION="<project-region>"      # e.g. westus
BACKEND_REGION="<backend-region>"      # must differ from PROJECT_REGION
SFX=$(date +%m%d%H%M)                   # helps keep names globally unique

az group create --name $RESOURCE_GROUP --location $PROJECT_REGION

az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file extensions/byom-cross-region/main.bicep \
  --parameters \
      location=$PROJECT_REGION \
      backendLocation=$BACKEND_REGION \
      apimName=apim-byom-$SFX \
      publisherEmail=you@example.com \
      "publisherName=Your Org" \
      backendAccountName=byombackend$SFX \
      projectMiClientId=00000000-0000-0000-0000-000000000000 \
      projectModelName=<model> projectModelVersion=<version> \
      projectModelSkuName=GlobalStandard projectModelCapacity=10 \
      backendModelDeployments='[{"name":"<model>","format":"OpenAI","version":"<version>","skuName":"GlobalStandard","capacity":10}]'
```

> Creating APIM StandardV2 takes ~30-45 min. `apimName` and `backendAccountName` must be globally unique.

### Set the real project identity

The deploy above configured APIM's `validate-azure-ad-token` policy with a **placeholder** client ID (`00000000-0000-0000-0000-000000000000`). Update it to the project managed identity's real **app (client) ID** so the gateway accepts the agent's token.

> **Do not re-run the full template to change this.** Once APIM is integrated with the `apim-outbound` subnet, a full redeploy fails with `InUseSubnetCannotBeDeleted` (the VNet can no longer be rewritten). Patch the APIM policy directly instead.

1. Get the project MI app (client) ID:

   ```bash
   PROJECT_ID=$(az deployment group show -g $RESOURCE_GROUP -n <deployment-name> \
     --query "properties.outputs.projectId.value" -o tsv)
   PRINCIPAL_ID=$(az resource show --ids "$PROJECT_ID" --query "identity.principalId" -o tsv)
   APP_ID=$(az ad sp show --id "$PRINCIPAL_ID" --query appId -o tsv)
   echo "project MI appId: $APP_ID"
   ```

2. Patch the APIM `/inference` policy's `<client-application-ids>`, swapping the placeholder for `$APP_ID`:

   ```bash
   BASE="https://management.azure.com/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ApiManagement/service/<apim-name>"

   # fetch current policy, replace the placeholder with the real app id
   az rest --method get \
     --url "$BASE/apis/inference/policies/policy?api-version=2022-08-01&format=rawxml" \
     --query "properties.value" -o tsv > policy.xml
   sed -i "s/00000000-0000-0000-0000-000000000000/$APP_ID/g" policy.xml

   # PUT the updated policy back
   python3 -c "import json;xml=open('policy.xml').read();json.dump({'properties':{'format':'rawxml','value':xml}}, open('policy_body.json','w'))"
   az rest --method put \
     --url "$BASE/apis/inference/policies/policy?api-version=2022-08-01" \
     --headers "Content-Type=application/json" --body @policy_body.json
   ```

The gateway now validates agent tokens against the real project identity.

---

## Step 2: Capture Deployment Outputs

The test reads its configuration from the deployment outputs.

```bash
DEPLOYMENT_NAME=$(az deployment group list -g $RESOURCE_GROUP \
  --query "[?contains(name, 'main')].name | [0]" -o tsv)

# Project (Foundry API) endpoint - build from the project name + account endpoint
PROJECT_NAME=$(az deployment group show -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME \
  --query "properties.outputs.projectName.value" -o tsv)

# APIM gateway URL and BYOM connection name
APIM_GATEWAY_URL=$(az deployment group show -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME \
  --query "properties.outputs.apimGatewayUrl.value" -o tsv)
BYOM_CONNECTION_NAME=$(az deployment group show -g $RESOURCE_GROUP -n $DEPLOYMENT_NAME \
  --query "properties.outputs.byomConnectionName.value" -o tsv)

# AI Services account endpoint
AI_SERVICES_NAME=$(az cognitiveservices account list -g $RESOURCE_GROUP --query "[0].name" -o tsv)
AI_ENDPOINT=$(az cognitiveservices account show -g $RESOURCE_GROUP -n $AI_SERVICES_NAME \
  --query "properties.endpoint" -o tsv)

export PROJECT_ENDPOINT="${AI_ENDPOINT%/}/api/projects/${PROJECT_NAME}"
export APIM_GATEWAY_URL
export BYOM_CONNECTION_NAME
export BYOM_DEPLOYMENT_NAME="gpt-5"     # or gpt-4o / gpt-5.1

echo "PROJECT_ENDPOINT=$PROJECT_ENDPOINT"
echo "APIM_GATEWAY_URL=$APIM_GATEWAY_URL"
echo "BYOM model = $BYOM_CONNECTION_NAME/$BYOM_DEPLOYMENT_NAME"
```

---

## Step 3: Verify APIM and Private Endpoints

```bash
# APIM service (StandardV2, VNet-integrated)
az apim list -g $RESOURCE_GROUP -o table

# Private endpoints - expect Storage, Cosmos, AI Search, Foundry, and the
# cross-region backend Foundry PE
az network private-endpoint list -g $RESOURCE_GROUP -o table
# Expected:
#   *-storage-private-endpoint
#   *-cosmosdb-private-endpoint
#   *-search-private-endpoint
#   *-private-endpoint            (project Foundry)
#   backend-pe...                 (cross-region backend Foundry)
```

The backend Foundry account and the project Foundry account both have `publicNetworkAccess = Disabled`; the model is never exposed on the public internet.

---

## Step 4: Create and Verify the BYOM (APIM) Connection

The agent does not talk to APIM by URL — it resolves a **project connection** that points at the APIM `/inference` API. That connection must exist **before** you run the agent test (the same way template 19 requires its A2A/MCP connections to exist before its agent tests).

### Where the connection is created

The [`extensions/byom-cross-region`](../extensions/byom-cross-region/) deployment creates it automatically. Its `main.bicep` calls the canonical connection module [`01-connections/apim/connection-apim.bicep`](../../01-connections/apim/connection-apim.bicep) as the `byomConnection` module and emits the name as the `byomConnectionName` output. So if you deployed via Step 1, the connection already exists — skip to verification.

### Option A - Create it standalone (existing APIM + Foundry)

If you already have APIM and the Foundry project deployed and only need the connection artifact, deploy the connection module directly:

```bash
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file ../../01-connections/apim/connection-apim.bicep \
  --parameters \
      projectResourceId="<foundry-project-resource-id>" \
      apimResourceId="<apim-resource-id>" \
      apiName="inference" \
      connectionName="$BYOM_CONNECTION_NAME" \
      authType="ProjectManagedIdentity" \
      isSharedToAll=true \
      deploymentInPath="true" \
      inferenceAPIVersion="$INFERENCE_API_VERSION"
```

> The extension passes a `staticModels` array (gpt-4o / gpt-5 / gpt-5.1) so the deployments surface in the portal. See [`01-connections/apim/samples/parameters-managed-identity-static-models.json`](../../01-connections/apim/samples/parameters-managed-identity-static-models.json) for the full shape.

### Verify the connection

Confirm the AI Gateway connection exists and surfaces the backend deployments as `<connection>/<deployment>`:

```bash
# List project connections (the AI Gateway connection should appear)
az cognitiveservices account connection list \
  -g $RESOURCE_GROUP -n $AI_SERVICES_NAME -o table 2>/dev/null || \
echo "Use the Foundry portal -> Project -> Management center -> Connected resources to verify '$BYOM_CONNECTION_NAME'."
```

In agent code the model is then referenced as, for example, `ai-gateway/gpt-5`.

---

## Step 5: Run the SDK Test

From a host with access to the VNet (or with public access temporarily enabled):

```bash
cd tests

# Run all tests (direct gateway probe + BYOM agent + fallback agent)
python test_byo_model_apim_gateway_agent_v2.py

# Only the BYOM agent path (the authoritative end-to-end test)
python test_byo_model_apim_gateway_agent_v2.py --test byom

# Only the project-region fallback model sanity check
python test_byo_model_apim_gateway_agent_v2.py --test basic
```

What each test validates:

| Test | Validates |
|------|-----------|
| `gateway_direct` | Direct HTTP reachability of the APIM `/inference` endpoint. **Only PASSES when run as the project managed identity** (the identity in APIM's `<client-application-ids>`); from a developer machine the token is rejected, so it reports **SKIPPED** rather than failing. |
| `agent_byom` | The full path: project agent -> BYOM connection -> APIM (managed identity) -> cross-region private endpoint -> backend Foundry model. **This is the authoritative test.** |
| `agent_basic` | The project endpoint and a project-region (local) deployment work at all. |

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| SDK call times out or connection refused | Foundry account is private. Connect via VPN / ExpressRoute / Bastion, or run from an in-VNet jump box. |
| `agent_byom` fails with **424 / Failed Dependency** | APIM or the cross-region private endpoint cannot reach the backend account. Check the `backend-pe` private endpoint DNS (`privatelink.openai.azure.com`, `privatelink.cognitiveservices.azure.com`, `privatelink.services.ai.azure.com`) and that APIM's managed identity has the token/role on the backend account. |
| `DeploymentNotFound` / **404** | `BYOM_CONNECTION_NAME` / `BYOM_DEPLOYMENT_NAME` don't match the deployed connection. Confirm with Step 4 (default `ai-gateway/gpt-5`). |
| `gateway_direct` returns **401/403** | Expected from a developer identity — the gateway validates the **project** managed identity. Rely on `agent_byom`. |
| `401` on `agent_basic` | Your identity lacks a data-plane role (e.g. **Azure AI User**) on the project. Assign it and retry. |

---

## Test Results Summary

| Test | Status |
|------|--------|
| `gateway_direct` (direct APIM probe) | ⬜ |
| `agent_byom` (BYOM via APIM gateway) | ⬜ |
| `agent_basic` (project-region model) | ⬜ |

Fill in ✅ / ❌ as you validate each path in your environment.
