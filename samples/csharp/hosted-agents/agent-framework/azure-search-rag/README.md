# What this sample demonstrates

A support specialist agent with Retrieval Augmented Generation (RAG) backed by **Azure AI Search**, the **Azure AI Search RAG Agent (Responses Protocol)** sample shows how to ground agent answers in a real keyword-indexed knowledge base using `TextSearchProvider` over `Azure.Search.Documents` via [Agent Framework](https://github.com/microsoft/agent-framework).

## How It Works

The agent uses a `TextSearchProvider` wired to an `Azure.Search.Documents.SearchClient`. Before each model invocation the framework runs a full-text search against the configured Azure AI Search index, retrieves the top three matching documents, injects them as context for the LLM, and the model composes an answer grounded in the retrieved information, reducing hallucination and ensuring responses reflect the actual product documentation.

> [!IMPORTANT]
> The agent assumes the search index already exists and is populated. It does **not** create or seed the index at runtime. See [Provisioning the search index](#provisioning-the-search-index) below for a one-time setup script that creates `contoso-outdoors` and seeds it with three Contoso Outdoors documents (return policy, shipping guide, tent care).

> [!NOTE]
> Provisioning of the Foundry project, model deployment, and the Azure AI Search service is handled by the [`azd-ai-starter-basic`](https://github.com/Azure-Samples/azd-ai-starter-basic) template, which `azd ai agent init` pulls in automatically. The chat model under `resources:` in `agent.manifest.yaml` flows into the starter's `AI_PROJECT_DEPLOYMENTS` parameter, and the `kind: tool` `id: azure_ai_search` entry flows into `AI_PROJECT_DEPENDENT_RESOURCES` to provision the Azure AI Search service plus a project-scoped connection.

> [!IMPORTANT]
> The starter's Azure AI Search bicep currently requires a co-provisioned storage account, but `azd ai agent init` does not auto-prompt for storage. After running `azd ai agent init`, manually edit the generated `azure.yaml` and add a `storage` entry to the agent's `resources:` array so both resources get provisioned together. See [Provisioning workaround](#provisioning-workaround-storage-dependency) below.

See [Program.cs](Program.cs) for the full implementation.

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)** (recommended)
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and the AI agent extension: `azd ext install azure.ai.agents`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

> [!NOTE]
> You do **not** need an existing [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry) project, model deployment, or Azure AI Search service to get started, `azd provision` creates them all for you. If you already have some of these, see the [note below](#using-azd-recommended-for-cli-workflows) on how to target them.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Chat model deployment name. Declared in `agent.manifest.yaml`. |
| `AZURE_SEARCH_ENDPOINT` | Yes | Azure AI Search service endpoint. Derived from `AZURE_AI_SEARCH_SERVICE_NAME` (auto-injected by the starter) via the binding in `agent.yaml`. Set manually only when running without `azd`. |
| `AZURE_SEARCH_INDEX_NAME` | Yes | Search index name. Defaults to `contoso-outdoors`. **Must exist before the agent starts** (see [Provisioning the search index](#provisioning-the-search-index)). |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

**Local development (without `azd`):**

```bash
# Set env vars directly. .NET does not natively read .env files.
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-chat-deployment-name>"
export AZURE_SEARCH_ENDPOINT="https://<search-service>.search.windows.net"
export AZURE_SEARCH_INDEX_NAME="contoso-outdoors"
```

> [!NOTE]
> When using `azd ai agent run`, environment variables are handled automatically. No manual setup needed.

### Installing Dependencies

> [!NOTE]
> If using `azd ai agent run`, dependencies are restored automatically. Skip to [Running the Sample](#running-the-sample).

Dependencies are restored automatically when building the project:

```bash
dotnet restore
```

### Running the Sample

The recommended way to run and test hosted agents locally is with the Azure Developer CLI (`azd`) or the Foundry Toolkit VS Code extension.

#### Using the Foundry Toolkit VS Code Extension

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Chat with a running agent using the **Agent Inspector**:

1. Start the agent locally first using **Using `azd`** or **Without `azd`** above. The agent listens on `http://localhost:8088/`.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
3. The Inspector auto-connects to the running agent. Send messages to chat with the agent and watch the streamed responses.

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd) (recommended for CLI workflows)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample, generates Bicep infrastructure, `agent.yaml`, and env config:

```bash
# Create a new folder for the agent and navigate into it
mkdir azure-search-rag-agent && cd azure-search-rag-agent

# Initialize from the manifest. azd reads it, downloads the sample,
# and generates Bicep infrastructure, agent.yaml, and env config
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/azure-search-rag/agent.manifest.yaml

# IMPORTANT: apply the storage workaround documented below before provisioning.

# Provision Azure resources (Foundry project, model deployment, Azure AI Search, storage, App Insights)
azd provision

# Run the agent locally (handles env vars, build, and startup)
azd ai agent run
```

##### Provisioning workaround: storage dependency

The starter's Azure AI Search bicep module currently requires a co-provisioned storage account, but `azd ai agent init` only auto-prompts for `azure_ai_search` and `bing_grounding` tool resources. After running `init`, open the generated `azure.yaml` and add a `storage` entry to the agent service's `resources:` array so both resources get provisioned together:

```yaml
services:
  azure-search-rag:
    config:
      resources:
        - resource: azure_ai_search
          connectionName: search
        - resource: storage
          connectionName: storage
```

Tracking issue: [Azure-Samples/azd-ai-starter-basic — make storage optional in azure_ai_search.bicep or auto-prompt](https://github.com/Azure-Samples/azd-ai-starter-basic/issues).

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/csharp/hosted-agents/agent-framework/azure-search-rag/agent.manifest.yaml`

> [!NOTE]
> If you already have a Foundry project, model deployment, and Azure AI Search service, add `-p <project-id> -d <deployment-name>` to `azd ai agent init` to target existing resources. You can also skip provisioning entirely and configure env vars manually, see [Without `azd`](#without-azd).

The agent starts on `http://localhost:8088/`. To invoke it:

```bash
azd ai agent invoke --local "What is your return policy?"
```

Or use curl directly:

```bash
curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "What is your return policy?", "stream": false}' | jq .

curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "How long does shipping take?", "stream": false}' | jq .

curl -sS -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "How do I clean my tent?", "stream": false}' | jq .
```

#### Without `azd`

If running without `azd`, set environment variables manually (see [Environment Variables](#environment-variables)), then:

```bash
dotnet run
```

### Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "What is your return policy?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

#### Deploying with the Foundry Toolkit VS Code Extension

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a tab-based **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate what it can.
2. If prompted, complete **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one) to deploy to.
3. On the **Basics** tab, configure the core deployment settings:
   - **Deployment Method**: **Code** (upload as a ZIP) or **Container** (Docker image via ACR).
   - For **Code**, pick a packaging option: **Remote** or **Local**.
   - For **Container**, pick a registry option: default ACR, your own ACR, or a prebuilt ACR image.
   - **Hosted Agent Name**: confirm the name to register with the hosting service.
4. On the **Review + Deploy** tab, finalize the runtime and resources:
   - Confirm the auto-detected runtime details (language, entry point, or Dockerfile).
   - Pick a **CPU and Memory** size.
   - Click **Deploy**. Fields are validated inline, and the extension handles the build/upload, agent version creation, and RBAC role assignment.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

## Provisioning the search index

The agent reads from a pre-existing index. Provision it once, before the first run, with the script below. The script is idempotent: it skips creation if the index already exists, and skips seeding if the index already has documents.

### Required schema

| Field | Type | Attributes |
|-------|------|------------|
| `id` | `Edm.String` | key, filterable |
| `content` | `Edm.String` | searchable |
| `sourceName` | `Edm.String` | filterable |
| `sourceLink` | `Edm.String` | (none) |

### Required search service authentication mode

The script and the agent runtime both authenticate to Azure AI Search via Entra ID (AAD) bearer tokens, **not** API keys. The search service must therefore have RBAC enabled. Services created before May 2024, or services explicitly provisioned with `disableLocalAuth=false` and `authOptions=null`, default to **API-key-only** auth and will return `403 Forbidden` to every AAD token regardless of RBAC role assignments.

Verify the current auth mode:

```bash
az search service show -g <rg> -n <search-service> \
  --query "{authOptions:authOptions, disableLocalAuth:disableLocalAuth}" -o json
```

The expected output is one of:

```json
{ "authOptions": { "aadOrApiKey": { "aadAuthFailureMode": "http403" } }, "disableLocalAuth": false }
```

or, for AAD-only:

```json
{ "authOptions": null, "disableLocalAuth": true }
```

If you see `"authOptions": null` together with `"disableLocalAuth": false`, RBAC is **off** and you must enable it before the script (or the agent) can authenticate. Flip the service to accept both AAD and API keys (safest, no breaking change for existing key consumers):

```bash
az search service update -g <rg> -n <search-service> \
  --auth-options aadOrApiKey --aad-auth-failure-mode http403
```

Or go AAD-only (rejects all API keys):

```bash
az search service update -g <rg> -n <search-service> --disable-local-auth true
```

Either change takes effect immediately on the control plane; allow ~1 minute for the data plane to pick it up.

### Required RBAC for the user running the script

Grant your user `Search Index Data Contributor` on the search service scope. This single role covers both index management (create) and document write (upload) for the bootstrap.

> [!IMPORTANT]
> Subscription `Owner`, `Contributor`, or `User Access Administrator` are **not sufficient on their own**. Those roles cover the management plane (deploy/scale/grant) but contain no `dataActions`, so REST calls to `/indexes/...` return `403`. The data-plane Search role must be granted explicitly even for subscription Owners.

```bash
SEARCH_ID=$(az search service show -g <rg> -n <search-service> --query id -o tsv)
USER_OID=$(az ad signed-in-user show --query id -o tsv)
az role assignment create --assignee-object-id $USER_OID --assignee-principal-type User \
  --role "Search Index Data Contributor" --scope $SEARCH_ID
```

### Bash one-shot using `curl` + `az` token

```bash
SEARCH_ENDPOINT="https://<search-service>.search.windows.net"
INDEX_NAME="contoso-outdoors"
TOKEN=$(az account get-access-token --resource https://search.azure.com --query accessToken -o tsv)

# Create the index (idempotent: 201 on create, 204 on update)
curl -sS -X PUT "${SEARCH_ENDPOINT}/indexes/${INDEX_NAME}?api-version=2024-07-01" \
  -H "Authorization: Bearer ${TOKEN}" -H "Content-Type: application/json" -d '{
    "name": "'"${INDEX_NAME}"'",
    "fields": [
      { "name": "id", "type": "Edm.String", "key": true, "filterable": true },
      { "name": "content", "type": "Edm.String", "searchable": true },
      { "name": "sourceName", "type": "Edm.String", "filterable": true },
      { "name": "sourceLink", "type": "Edm.String" }
    ]
  }'

# Seed three Contoso Outdoors documents
curl -sS -X POST "${SEARCH_ENDPOINT}/indexes/${INDEX_NAME}/docs/index?api-version=2024-07-01" \
  -H "Authorization: Bearer ${TOKEN}" -H "Content-Type: application/json" -d '{
    "value": [
      {
        "@search.action": "mergeOrUpload",
        "id": "return-policy",
        "sourceName": "Contoso Outdoors Return Policy",
        "sourceLink": "https://contoso.com/policies/returns",
        "content": "Customers may return any item within 30 days of delivery. Items should be unused and include original packaging. Refunds are issued to the original payment method within 5 business days of inspection."
      },
      {
        "@search.action": "mergeOrUpload",
        "id": "shipping-guide",
        "sourceName": "Contoso Outdoors Shipping Guide",
        "sourceLink": "https://contoso.com/help/shipping",
        "content": "Standard shipping is free on orders over $50 and typically arrives in 3-5 business days within the continental United States. Expedited options are available at checkout."
      },
      {
        "@search.action": "mergeOrUpload",
        "id": "tent-care",
        "sourceName": "TrailRunner Tent Care Instructions",
        "sourceLink": "https://contoso.com/manuals/trailrunner-tent",
        "content": "Clean the tent fabric with lukewarm water and a non-detergent soap. Allow it to air dry completely before storage and avoid prolonged UV exposure to extend the lifespan of the waterproof coating."
      }
    ]
  }'
```

### Required RBAC for the agent runtime

The hosted agent runs under its own managed identity. Grant that identity `Search Index Data Reader` on the search service scope so it can query the index at runtime:

```bash
# Look up the agent MI principal id from the deployed agent version.
TOK=$(az account get-access-token --resource https://ai.azure.com --query accessToken -o tsv)
MI=$(curl -sS -H "Authorization: Bearer $TOK" \
  "https://<account>.services.ai.azure.com/api/projects/<project>/agents/azure-search-rag?api-version=v1" \
  | jq -r '.versions.latest.instance_identity.principal_id')

az role assignment create --assignee-object-id $MI --assignee-principal-type ServicePrincipal \
  --role "Search Index Data Reader" --scope $SEARCH_ID
```

Wait ~3 minutes for AAD propagation before invoking the agent.

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
