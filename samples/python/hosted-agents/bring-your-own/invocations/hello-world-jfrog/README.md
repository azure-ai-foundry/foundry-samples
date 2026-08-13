<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency note for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# What this sample demonstrates

The same minimal "hello world" hosted agent as [`hello-world`](../hello-world/), but deployed from a **private JFrog Artifactory registry** instead of Azure Container Registry.

It shows how Microsoft Foundry pulls a private image using **Microsoft Entra workload identity federation** — your Foundry project's managed identity is exchanged for a short-lived JFrog access token at pull time.

> [!IMPORTANT]
> **No JFrog password, API key, reference token, or Docker `auth` value is ever stored in Azure.** Those credentials are only used from your own machine to *push* images. Foundry authenticates with a short-lived token obtained through OIDC token exchange.

If you don't need a private third-party registry, start with [`hello-world`](../hello-world/) instead — it's simpler and deploys end-to-end with a single `azd deploy`.

> [!NOTE]
> **Two steps in this guide use the REST API instead of `azd`** — creating the registry connection
> (Step 4) and creating the agent version that references it (Step 8). This is a **temporary gap in
> the tooling, not in the service**, and the team is working to make this sample completely
> deployable via `azd`. See [The temporary gap](#the-temporary-gap) for details.

## How it works

```
                 ┌─────────────────────────┐
                 │  Foundry project        │
                 │  (system-assigned MI)   │
                 └───────────┬─────────────┘
                             │ 1. Entra token
                             │    aud = <your Entra app>
                             │    oid = <project MI object ID>
                             ▼
                 ┌─────────────────────────┐
                 │  JFrog OIDC provider    │  2. validates issuer + audience,
                 │  + identity mapping     │     matches the oid claim
                 └───────────┬─────────────┘
                             │ 3. short-lived JFrog access token
                             ▼
                 ┌─────────────────────────┐
                 │  JFrog Docker repo      │  4. Foundry pulls the image
                 └─────────────────────────┘
```

The **Entra app registration is only an audience identifier** — it needs no client secret, no redirect URI, and no API permissions.

## Prerequisites

| Requirement | Notes |
|---|---|
| Foundry project with a **system-assigned managed identity** | Required. This identity is what JFrog trusts. |
| JFrog Artifactory with a **Docker repository** | You need one-time **Administer** rights to configure OIDC. |
| Entra app registration | Audience only — no secret needed. |
| **Azure AI Developer** role on the Foundry project | To create connections and deploy. |
| `azd`, Azure CLI, Docker | [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25+), then `azd auth login` and `az login`. |

---

# Part 1 — One-time setup

Do this once per Foundry project. Steps 1–3 usually need an administrator.

## Step 1. Create the Entra app registration (audience only)

Azure portal → **Microsoft Entra ID** → **App registrations** → **New registration**.

- Name: anything, e.g. `foundry-jfrog-audience`
- Supported account types: **Single tenant**
- Redirect URI: **leave blank**

Do **not** create a client secret.

➡️ Copy the **Application (client) ID** — this is your `<audience>`.

## Step 2. Get your Foundry project's identity

Foundry portal → your project → **Identity** → confirm **System assigned** is **On**.

Or with the CLI:

```bash
az rest --method get \
  --url "https://management.azure.com/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>?api-version=2025-10-01-preview" \
  --query identity.principalId -o tsv
```

➡️ Copy the **principal (object) ID** — this is your `<project-identity>`.

> [!WARNING]
> This is the **project's** identity, not the object ID of the app registration from Step 1. Confusing the two is the single most common cause of image-pull failures.

## Step 3. Configure OIDC in JFrog

JFrog → **Administration** → **General Management** → **Manage Integrations** → **OIDC**.

**3a. Create the provider**

| Field | Value |
|---|---|
| Provider type | `Azure` |
| Issuer URL | `https://login.microsoftonline.com/<tenant-id>/v2.0` |
| Audience | `<audience>` from Step 1 |

➡️ Note the **provider name**.

**3b. Add an identity mapping** on that provider:

- **Claims:**
  ```json
  {
    "oid": "<project-identity>",
    "aud": "<audience>",
    "iss": "https://login.microsoftonline.com/<tenant-id>/v2.0"
  }
  ```
- **Token scope:** a JFrog group with **read** permission on your Docker repository
- **Expiry:** ~3600 seconds

## Step 4. Create the Foundry registry connection

```powershell
./deploy/create-registry-connection.ps1 `
    -SubscriptionId   <sub> `
    -ResourceGroup    <rg> `
    -AccountName      <foundry-account> `
    -ProjectName      <project> `
    -JFrogHost        mytenant.jfrog.io `
    -JFrogRepository  docker-local `
    -OidcAudience     <audience> `
    -OidcProviderName <provider-name>
```

This creates a connection named `jfrog-oidc-registry` containing only non-secret OIDC settings.

> [!NOTE]
> **Creating the registry connection through the REST API is temporary.** `azd` cannot declare a
> registry connection today, so this script calls the REST API on your behalf. The team is working
> to make this completely deployable via `azd` — once that lands, the connection will be declared
> in `azure.yaml` and this script will no longer be needed.

✅ Setup is complete and never needs repeating.

---

# Part 2 — Deploy

## Step 5. Build and push the image to JFrog

```bash
cd src/hello-world-jfrog-invocations

docker build --platform=linux/amd64 -t hello-world-jfrog-invocations:1.0.0 .

docker login mytenant.jfrog.io
docker tag hello-world-jfrog-invocations:1.0.0 \
  mytenant.jfrog.io/docker-local/hello-world-jfrog-invocations:1.0.0
docker push mytenant.jfrog.io/docker-local/hello-world-jfrog-invocations:1.0.0
```

> [!IMPORTANT]
> Always build with `--platform=linux/amd64`. Images built natively on Apple Silicon or other ARM64 machines will fail at runtime.

## Step 6. Update `azure.yaml`

Set the `image` field to the tag you just pushed:

```yaml
image: mytenant.jfrog.io/docker-local/hello-world-jfrog-invocations:1.0.0
```

Leave `project` and `language` **empty** — that is what tells azd to use your pre-built image instead of rebuilding from source.

## Step 7. Provision and deploy

```bash
azd provision   # creates the Foundry project, model deployment, App Insights
azd deploy
```

`azd deploy` registers the agent. **It will then report a failure — this is expected today.** See the note below.

## Step 8. Create the version that references the connection

```powershell
./deploy/create-agent-version.ps1 `
    -ProjectEndpoint https://<account>.services.ai.azure.com/api/projects/<project> `
    -AgentName       hello-world-jfrog-invocations `
    -Image           mytenant.jfrog.io/docker-local/hello-world-jfrog-invocations:1.0.0 `
    -ModelDeployment gpt-5.4-mini
```

This creates the agent version with `registry_connection_id` set, waits for it to become ready, and routes the endpoint to it.

> [!NOTE]
> **This REST API step is temporary.** It exists only because `azd deploy` cannot yet attach the
> registry connection to the agent version. The team is working to make this completely deployable
> via `azd`; when that ships, Steps 7 and 8 merge into a single `azd deploy` and this script is
> deleted from the sample.

## Step 9. Invoke

```bash
azd ai agent invoke "What is Microsoft Foundry?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

---

# The temporary gap

> [!NOTE]
> **The Foundry service fully supports pulling images from private registries such as JFrog.** The gap is only in the tooling: `azd` (and the `azure-ai-projects` SDK) cannot yet declare a registry connection or reference one from an agent version, so `azure.yaml` alone can't authorize the pull. That's why Steps 4 and 8 use the REST API.
>
> **The team is actively working to make this completely deployable via `azd`.** Once support lands, the connection will be declared in `azure.yaml`, Steps 7–8 collapse into a single `azd deploy`, and both helper scripts go away.

**What's happening under the covers:** the agent version needs this field, which only the REST API accepts today:

```json
"container_configuration": {
  "image": "mytenant.jfrog.io/docker-local/hello-world-jfrog-invocations:1.0.0",
  "registry_connection_id": "jfrog-oidc-registry"
}
```

Without it, Foundry attempts an anonymous pull and provisioning fails.

---

# Verifying it worked

A successful pull produces this in the platform logs:

```
Private registry pull starting for hello-world-jfrog-invocations:<v>
  (connection=jfrog-oidc-registry, host=mytenant.jfrog.io)
Private registry pull credential resolved and attached
```

> [!TIP]
> A version may briefly report `status: active` before turning `failed`. Confirm that `container_protocol_versions` is **non-empty** before treating a deployment as successful — `create-agent-version.ps1` already checks this for you.

# Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Provisioning fails; logs show `buildah pull failed with exit code 125` at `steps/CreateAdcSnapshot` | Foundry could not authenticate to JFrog | The connection wasn't attached (Step 8), or the JFrog identity mapping uses the wrong object ID (Step 2/3b) |
| `azd deploy` reports *"agent deployment timed out (last status: creating)"* | azd stops polling after ~5 minutes | Cosmetic — provisioning often continues and succeeds. Check the real status in the portal |
| `failed to fetch builder image .../oryx/builder` | `project` / `language` are not empty in `azure.yaml` | Set both to `''` (Step 6) |
| `registry_connection_id must be a valid workspace connection name` | The full ARM resource ID was passed | Use the connection **name**, e.g. `jfrog-oidc-registry` |
| Agent provisions but crashes at startup | Missing Python dependency | Check `azd ai agent monitor` output and add it to `requirements.txt` |
| Image runs locally but fails in Foundry | Image built for ARM64 | Rebuild with `--platform=linux/amd64` |

# Related

- [`hello-world`](../hello-world/) — the same agent deployed from ACR with a single `azd deploy`
- [Hosted agents overview](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [Hosted agent quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd)
