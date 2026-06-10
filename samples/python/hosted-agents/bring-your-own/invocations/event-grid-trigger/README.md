# What this sample demonstrates

A **Bring Your Own** [Invocations protocol](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents) hosted agent that is **event-driven from Azure Storage**, with Event Grid POSTing **directly** to the agent. Event Grid authenticates the delivery with the Event Grid system topic's **system-assigned managed identity (SAMI)** whose AAD audience is set to `https://ai.azure.com` (the Foundry data plane), so the agent's standard token validation accepts the request. The agent then reads the blob with its **per-agent Microsoft Entra identity**, summarizes it with a Foundry model, and writes the result as `<name>.summary.json` to a **separate summary container** so the full pipeline is verifiable from Storage alone.

End-to-end flow:

```
user uploads blob → input container → Event Grid system topic
   → EG delivery (system topic SAMI mints AAD token for https://ai.azure.com)
       → POST EG event batch to <agent-invocations-url>
           → agent extracts (container, name) from data.url,
             downloads blob (per-agent MI),
             calls model to summarize,
             writes <name>.summary.json to the summary container
                ↳ also logged to stdout (azd ai agent monitor)
```

Using a **sibling output container** instead of the input container is what keeps the pipeline loop-free: the Event Grid subscription is scoped to the input container, so writes to the summary container never re-fire it.

This is the canonical **event-driven Azure** pattern for hosted agents. It complements [`09-downstream-azure`](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/09-downstream-azure/README.md), which shows a chat-driven agent calling Azure data-plane services; here, an Azure event source pushes work *into* the agent.

## How It Works

### Authentication

Event Grid supports [delivery with managed identity](https://learn.microsoft.com/en-us/azure/event-grid/managed-service-identity): each event POST carries an AAD bearer token minted from a managed identity attached to the topic. By enabling a **system-assigned managed identity (SAMI)** on the system topic, setting the delivery audience to `https://ai.azure.com`, and giving that SAMI the **Foundry User** role on the Foundry project, the agent's invocations endpoint accepts the call as if it came from any other Foundry caller — no separate identity resource, no shared secrets, and no agent-side code to verify the EG handshake header.

### The handler

See [`main.py`](main.py). The handler accepts three POST shapes:

1. **Event Grid `SubscriptionValidationEvent`** — answered with `{"validationResponse": "<code>"}` so the EG subscription can finish provisioning.
2. **Event Grid `Microsoft.Storage.BlobCreated` batch** — the container and blob name are extracted from `data.url`.
3. **Direct `{"container": "...", "name": "..."}`** — useful for quick local invokes via `azd ai agent invoke`.

For (2) and (3), the agent downloads the blob (truncated to 64 KiB), summarizes it with the Foundry Responses API, writes `<name>.summary.json` to the configured summary container (`AZURE_STORAGE_SUMMARY_CONTAINER_NAME`), and logs `event-grid-trigger:summary blob=<container>/<name> output=<sum-container>/<name>.summary.json …`. Stream the logs with `azd ai agent monitor`, or just open the summary container in the Azure portal or Storage Explorer.

> The summary container is intentionally **different** from the input container. The Event Grid subscription is scoped to the input container, so writes to a sibling container do not re-trigger the agent. Do not point the agent's output at the same container the EG subscription watches.

## Prerequisites

In addition to the prerequisites listed in the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/README.md), this sample requires:

- An **Azure Storage account** with two existing containers: one for **inputs** (the Event Grid subscription watches this one) and a separate one for **summaries** the agent writes back. The two must be distinct so writes to the summary container don't re-trigger the agent.

Set the following shell variables — the rest of the commands below assume them.

Bash:

```bash
RG="<your-resource-group>"
AZURE_STORAGE_ACCOUNT_NAME="<your-storage-account-name>"
INPUT_CONTAINER="<your-input-container>"
SUMMARY_CONTAINER="<your-summary-container>"
FOUNDRY_ACCOUNT_NAME="<your-foundry-account-name>"
FOUNDRY_PROJECT_NAME="<your-foundry-project-name>"
```

PowerShell:

```powershell
$RG = "<your-resource-group>"
$AZURE_STORAGE_ACCOUNT_NAME = "<your-storage-account-name>"
$INPUT_CONTAINER = "<your-input-container>"
$SUMMARY_CONTAINER = "<your-summary-container>"
$FOUNDRY_ACCOUNT_NAME = "<your-foundry-account-name>"
$FOUNDRY_PROJECT_NAME = "<your-foundry-project-name>"
```

## 1. Deploy the agent

[agent.yaml](agent.yaml) declares two environment variables and binds each value to an `${...}` placeholder that `azd` resolves from the **azd environment** at deploy time (your shell's `export` / `$env:` values are not propagated to the deployed agent). Set them once with `azd env set` before deploying:

```powershell
azd env set AZURE_STORAGE_ACCOUNT_NAME "<storage-account-name>"
azd env set AZURE_STORAGE_SUMMARY_CONTAINER_NAME "<summary-container-name>"
```

Then follow the [Deploy any sample](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/README.md#deploy-any-sample) section in the parent README:

```powershell
azd deploy
```

After deployment capture the **Agent Invocations URL** — `https://<host>/agents/<name>/endpoint/protocols/invocations?api-version=2025-11-15-preview` — printed by `azd ai agent show`.

## 2. Grant the per-agent identity blob and Foundry access

The per-agent identity needs three role assignments:

- **Storage Blob Data Reader** on the **input** container — to download the uploaded blob.
- **Storage Blob Data Contributor** on the **summary** container — to write `<name>.summary.json`.
- **Foundry User** on the **Foundry project** — to call the Responses API that produces the summary. The agent calls the Foundry project data plane with its own MI; without this role the call is rejected with `401 PermissionDenied: Principal does not have access to API/Operation`.

`azd ai agent show` returns the per-agent identity's object id under `instance_identity.principal_id`; capture it together with the storage account and project scopes, then reuse them in the assignment commands below.

Bash:

```bash
PRINCIPAL_ID=$(azd ai agent show -o json | jq -r '.instance_identity.principal_id')
ACCOUNT_ID=$(az storage account show -n "$AZURE_STORAGE_ACCOUNT_NAME" --query id -o tsv)
PROJECT_ID="$(az cognitiveservices account show -n "$FOUNDRY_ACCOUNT_NAME" -g "$RG" --query id -o tsv)/projects/$FOUNDRY_PROJECT_NAME"

az role assignment create \
  --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Reader" \
  --scope "$ACCOUNT_ID/blobServices/default/containers/$INPUT_CONTAINER"

az role assignment create \
  --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" \
  --scope "$ACCOUNT_ID/blobServices/default/containers/$SUMMARY_CONTAINER"

az role assignment create \
  --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Foundry User" --scope "$PROJECT_ID"
```

PowerShell:

```powershell
$PRINCIPAL_ID = (azd ai agent show -o json | ConvertFrom-Json).instance_identity.principal_id
$ACCOUNT_ID = az storage account show -n $AZURE_STORAGE_ACCOUNT_NAME --query id -o tsv
$PROJECT_ID = "$(az cognitiveservices account show -n $FOUNDRY_ACCOUNT_NAME -g $RG --query id -o tsv)/projects/$FOUNDRY_PROJECT_NAME"

az role assignment create `
  --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --role "Storage Blob Data Reader" `
  --scope "$ACCOUNT_ID/blobServices/default/containers/$INPUT_CONTAINER"

az role assignment create `
  --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --role "Storage Blob Data Contributor" `
  --scope "$ACCOUNT_ID/blobServices/default/containers/$SUMMARY_CONTAINER"

az role assignment create `
  --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --role "Foundry User" --scope $PROJECT_ID
```

Role assignments take a minute or two to propagate.

## 3. Create the Event Grid system topic with a system-assigned identity

Create the topic on the storage account with **SAMI enabled**, then grant the topic's identity **Foundry User** on the Foundry project so the bearer tokens it mints for the `https://ai.azure.com` audience are accepted by the agent's invocations endpoint.

Bash:

```bash
TOPIC="<your-system-topic-name>"
SOURCE_ID=$(az storage account show -n "$AZURE_STORAGE_ACCOUNT_NAME" --query id -o tsv)
TOPIC_LOCATION=$(az storage account show -n "$AZURE_STORAGE_ACCOUNT_NAME" --query location -o tsv)

az eventgrid system-topic create \
  -g "$RG" -n "$TOPIC" -l "$TOPIC_LOCATION" \
  --topic-type microsoft.storage.storageaccounts --source "$SOURCE_ID" \
  --identity systemassigned

TOPIC_PRINCIPAL_ID=$(az eventgrid system-topic show -g "$RG" -n "$TOPIC" --query identity.principalId -o tsv)
PROJECT_ID="$(az cognitiveservices account show -n "$FOUNDRY_ACCOUNT_NAME" -g "$RG" --query id -o tsv)/projects/$FOUNDRY_PROJECT_NAME"

az role assignment create \
  --assignee-object-id "$TOPIC_PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Foundry User" --scope "$PROJECT_ID"
```

PowerShell:

```powershell
$TOPIC = "<your-system-topic-name>"
$SOURCE_ID = az storage account show -n $AZURE_STORAGE_ACCOUNT_NAME --query id -o tsv
$TOPIC_LOCATION = az storage account show -n $AZURE_STORAGE_ACCOUNT_NAME --query location -o tsv

az eventgrid system-topic create `
  -g $RG -n $TOPIC -l $TOPIC_LOCATION `
  --topic-type microsoft.storage.storageaccounts --source $SOURCE_ID `
  --identity systemassigned

$TOPIC_PRINCIPAL_ID = az eventgrid system-topic show -g $RG -n $TOPIC --query identity.principalId -o tsv
$PROJECT_ID = "$(az cognitiveservices account show -n $FOUNDRY_ACCOUNT_NAME -g $RG --query id -o tsv)/projects/$FOUNDRY_PROJECT_NAME"

az role assignment create `
  --assignee-object-id $TOPIC_PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --role "Foundry User" --scope $PROJECT_ID
```

## 4. Create the event subscription with SAMI delivery

Tell Event Grid to deliver to the agent's invocations URL as a webhook, authenticated by the system topic's SAMI with audience `https://ai.azure.com`.

> The `az eventgrid system-topic event-subscription create` CLI does **not** expose the `deliveryWithResourceIdentity` property needed to attach the SAMI to delivery. Create the subscription with `az rest` (a direct ARM PUT) instead. The ARM resource path is `…/systemTopics/<topic>/eventSubscriptions/<sub-name>`.

Bash:

```bash
SUB_NAME="blob-to-agent"
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
AGENT_URL=$(azd ai agent show -o json | jq -r '.agent_endpoints.invocations')

cat > eg-sub.json <<EOF
{
  "properties": {
    "deliveryWithResourceIdentity": {
      "identity": { "type": "SystemAssigned" },
      "destination": {
        "endpointType": "WebHook",
        "properties": {
          "endpointUrl": "$AGENT_URL",
          "azureActiveDirectoryTenantId": "$TENANT_ID",
          "azureActiveDirectoryApplicationIdOrUri": "https://ai.azure.com"
        }
      }
    },
    "filter": {
      "includedEventTypes": ["Microsoft.Storage.BlobCreated"],
      "subjectBeginsWith": "/blobServices/default/containers/$INPUT_CONTAINER/"
    },
    "eventDeliverySchema": "EventGridSchema"
  }
}
EOF

az rest --method put \
  --url "https://management.azure.com/subscriptions/$SUB_ID/resourceGroups/$RG/providers/Microsoft.EventGrid/systemTopics/$TOPIC/eventSubscriptions/$SUB_NAME?api-version=2025-07-15-preview" \
  --headers "Content-Type=application/json" \
  --body @eg-sub.json
```

PowerShell:

```powershell
$SUB_NAME = "blob-to-agent"
$SUB_ID = az account show --query id -o tsv
$TENANT_ID = az account show --query tenantId -o tsv
$AGENT_URL = (azd ai agent show -o json | ConvertFrom-Json).agent_endpoints.invocations

$body = @{
  properties = @{
    deliveryWithResourceIdentity = @{
      identity = @{ type = "SystemAssigned" }
      destination = @{
        endpointType = "WebHook"
        properties = @{
          endpointUrl = $AGENT_URL
          azureActiveDirectoryTenantId = $TENANT_ID
          azureActiveDirectoryApplicationIdOrUri = "https://ai.azure.com"
        }
      }
    }
    filter = @{
      includedEventTypes = @("Microsoft.Storage.BlobCreated")
      subjectBeginsWith = "/blobServices/default/containers/$INPUT_CONTAINER/"
    }
    eventDeliverySchema = "EventGridSchema"
  }
} | ConvertTo-Json -Depth 10

$body | Out-File -FilePath eg-sub.json -Encoding ascii

$url = "https://management.azure.com/subscriptions/$SUB_ID/resourceGroups/$RG/providers/Microsoft.EventGrid/systemTopics/$TOPIC/eventSubscriptions/${SUB_NAME}?api-version=2025-07-15-preview"

az rest --method put --url $url `
  --headers "Content-Type=application/json" `
  --body "@eg-sub.json"
```

> The `@filename` syntax tells `az rest` to read the body from a file and set the correct `Content-Type` header. Passing a JSON string directly via `--body $var` in PowerShell can drop the content-type and yield `UnsupportedMediaType`.

During the PUT, Event Grid POSTs a one-time `SubscriptionValidationEvent` to the agent's invocations URL; the handler answers it with `{"validationResponse": "<code>"}` and the subscription transitions to `provisioningState: Succeeded`. Confirm with `az rest` (the `az eventgrid` CLI uses an older API version that cannot read subscriptions configured with `deliveryWithResourceIdentity`):

```powershell
az rest --method get --url $url --query "properties.provisioningState" -o tsv
```

## 5. Try it & verify

Upload a `.txt` (or `.md`) blob:

Bash:

```bash
echo "Hosted agents process Event Grid blob-created events end to end via system-assigned MI delivery." > hello.txt
az storage blob upload \
  --account-name "$AZURE_STORAGE_ACCOUNT_NAME" \
  -c "$INPUT_CONTAINER" -f hello.txt -n hello.txt --auth-mode login
```

PowerShell:

```powershell
"Hosted agents process Event Grid blob-created events end to end via system-assigned MI delivery." | Set-Content hello.txt
az storage blob upload `
  --account-name $AZURE_STORAGE_ACCOUNT_NAME `
  -c $INPUT_CONTAINER -f hello.txt -n hello.txt --auth-mode login
```

Within a few seconds a corresponding summary blob should appear in the sibling container:

Bash:

```bash
az storage blob download \
  --account-name "$AZURE_STORAGE_ACCOUNT_NAME" \
  -c "$SUMMARY_CONTAINER" -n hello.txt.summary.json --auth-mode login -f - | cat
```

PowerShell:

```powershell
az storage blob download `
  --account-name $AZURE_STORAGE_ACCOUNT_NAME `
  -c $SUMMARY_CONTAINER -n hello.txt.summary.json --auth-mode login -f hello.txt.summary.json
Get-Content hello.txt.summary.json -Raw
```

Expected payload:

```json
{
  "input": "<input-container>/hello.txt",
  "elapsed_ms": 842,
  "truncated": false,
  "summary": "- Hosted agents …\n- …"
}
```

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| EG subscription provisioning fails with `Webhook validation handshake failed` | The agent didn't return the `validationResponse`. Confirm the deployed `main.py` includes the `_extract_subscription_validation_event` branch (`azd deploy`), and that the system topic's SAMI has **Foundry User** on the Foundry project so EG's token is accepted. |
| `401 Unauthorized` from the agent on real events | The system topic's SAMI is missing **Foundry User** on the Foundry project, or the subscription is configured with the wrong audience (must be `https://ai.azure.com`) or wrong tenant id. |
| `az eventgrid system-topic show --query identity.principalId` is empty | SAMI wasn't enabled on the topic. Re-run `az eventgrid system-topic create ... --identity systemassigned` (or `az eventgrid system-topic update --identity systemassigned`) and recheck. |
| Agent trace shows `401 PermissionDenied: Principal does not have access to API/Operation` | Per-agent identity is missing **Foundry User** on the Foundry project (needed to call the Responses API that summarizes). Assign it in step 2. |
| Agent returns `AuthorizationPermissionMismatch` reading the blob | Per-agent identity is missing **Storage Blob Data Reader** on the input container. |
| Summary blob is never written | Per-agent identity is missing **Storage Blob Data Contributor** on the summary container, or the container does not exist. |
| Agent fires twice per upload | Summary is being written into the **same** container the EG subscription watches — set `AZURE_STORAGE_SUMMARY_CONTAINER_NAME` to a different container. |
| `System topic's location must match with location of the source resource` | Create the system topic in the storage account's region (step 3 reads it via `az storage account show --query location`). |

## See also

- [Deliver events using managed identity](https://learn.microsoft.com/en-us/azure/event-grid/managed-service-identity) — the Event Grid docs for the managed-identity delivery pattern this sample uses.
- [`09-downstream-azure`](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/09-downstream-azure/README.md) — the per-agent-identity + Azure RBAC pattern in a chat-driven (not event-driven) agent.
