<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# What this sample demonstrates

A **Bring Your Own** hosted agent using the **Invocations protocol** with **Azure AI Foundry Toolbox MCP** integration in Python. It shows how to connect to a Foundry toolbox at startup, discover available tools via MCP, and let the model call them during conversation through an agentic tool-calling loop.

This sample combines:
- The [`azure-ai-agentserver-invocations`](https://pypi.org/project/azure-ai-agentserver-invocations/) SDK for the Invocations protocol
- The [Foundry SDK (`azure-ai-projects`)](https://pypi.org/project/azure-ai-projects/) for model access via the Responses API
- Direct MCP (JSON-RPC over HTTP) for toolbox tool discovery and invocation

> **Invocations vs Responses:** Unlike the Responses protocol, the Invocations protocol does **not** provide built-in server-side conversation history. This agent maintains an in-memory session store keyed by `agent_session_id`. In production, replace it with durable storage (Redis, Cosmos DB, etc.) so history survives restarts.

## How It Works

### Toolbox Integration

At startup, the agent connects to the toolbox MCP endpoint, runs `initialize` + `tools/list`, and converts the discovered tools into function definitions for the Responses API. When the model requests a tool call, the agent executes it via MCP `tools/call` and feeds the result back to the model.

### Model Integration

The agent uses the Foundry SDK Responses API with tool definitions. The agentic loop handles multi-step tool calling — the model can call tools multiple times before producing a final text answer.

### Agent Hosting

The agent is hosted using the [Azure AI AgentServer Invocations SDK](https://pypi.org/project/azure-ai-agentserver-invocations/), which provisions a REST API endpoint compatible with the Azure AI Invocations protocol.

### Agent Deployment

The hosted agent can be developed and deployed to Microsoft Foundry using the [Azure Developer CLI](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd).

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)** (recommended)
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25 or later) and the unified Foundry CLI extension bundle: `azd ext install microsoft.foundry` (if you previously installed `azure.ai.agents` or `azure.ai.toolboxes`, run `azd ext uninstall <name>` first).
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **Python 3.10 or later**
   - Verify your version: `python --version`

4. **A Foundry Toolbox**
   - Create a toolbox in your Foundry project (see [Create the toolbox with `azd ai`](#create-the-toolbox-with-azd-ai) below).

### Create the toolbox with `azd ai`

> [!TIP]
> If you use GitHub Copilot for Azure to scaffold a hosted agent that consumes this toolbox, the following skill references describe the same endpoint contract (env var, headers, MCP protocol, citation patterns, and troubleshooting) that the agent must implement:
>
> - [Toolbox reference](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/microsoft-foundry/foundry-agent/create/references/toolbox-reference.md) — endpoint format, MCP protocol, OAuth consent handling, citation patterns, and troubleshooting.
> - [Use toolbox in a hosted agent](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/microsoft-foundry/foundry-agent/create/references/use-toolbox-in-hosted-agent.md) — endpoint resolution, env-var contract, payload shape, code integration patterns, and tracing.

The agent reads `TOOLBOX_ENDPOINT` (a complete MCP URL) at startup. `azd ai agent init` + `azd up` will create the toolbox declared in [`agent.manifest.yaml`](agent.manifest.yaml) automatically. To create or manage the toolbox directly with `azd` (using the unified `microsoft.foundry` extension), follow these steps:

1. Point `azd` at your Foundry project (once per shell):

   ```bash
   export PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
   azd ai project set $PROJECT_ENDPOINT
   ```

2. (Connections.) This sample's default tool — `web_search` — is built-in and does not require a project connection. If you extend the sample to use MCP, Azure AI Search, or Bing Custom Search, create the corresponding connection first. The command shape is:

   ```bash
   azd ai connection create <name> \
     --kind <remote-tool|cognitive-search|GroundingWithCustomSearch> \
     --target <endpoint-url> \
     --auth-type <none|custom-keys|api-key|oauth2|user-entra-token|project-managed-identity|agentic-identity> \
     [--custom-key "Header=Value" | --key <key> | --client-id ... --client-secret ... --authorization-url ... --token-url ... | --audience <aad-resource-uri>]
   ```

   Inspect with `azd ai connection list` / `azd ai connection show <name>`; remove with `azd ai connection delete <name> --force`.

3. Author a `toolbox.yaml` that lists the tools (and any connection names they reference):

   ```yaml
   # toolbox.yaml
   description: Web search tools for the BYO invocations sample
   tools:
     - type: web_search
       name: web
   ```

4. Create the toolbox from that file:

   ```bash
   azd ai toolbox create web-search-tools --from-file ./toolbox.yaml
   ```

   The first version becomes the default automatically. Manage with `azd ai toolbox list`, `azd ai toolbox show web-search-tools`, `azd ai toolbox version list web-search-tools`, and `azd ai toolbox delete web-search-tools --force`.

   To stage incremental changes safely, use `azd ai toolbox connection add/remove` and `azd ai toolbox skill add/list/remove` &mdash; each creates a new toolbox version that carries forward existing connections and skills but **doesn't** change the default. Promote a version with `azd ai toolbox publish web-search-tools <version>` when you're ready to make it active.

5. Retrieve the MCP endpoint and expose it to the agent as `TOOLBOX_ENDPOINT`:

   ```bash
   azd ai toolbox show web-search-tools --output json
   azd env set TOOLBOX_ENDPOINT "https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/web-search-tools/mcp?api-version=v1"
   ```

   For local runs, put the same URL into `.env` (see [Environment Variables](#environment-variables) below).

### Environment Variables

See [`.env.example`](.env.example) or `.env` for the full list of environment variables this sample uses.

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name — must match your Foundry project deployment. Declared in `agent.manifest.yaml`. |
| `TOOLBOX_ENDPOINT` | Yes | Full toolbox MCP endpoint URL including toolbox name and `?api-version=v1`. Declared in `agent.manifest.yaml`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

`TOOLBOX_ENDPOINT` must be the complete MCP URL for your toolbox. Two forms are supported:
```
# Latest version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/mcp?api-version=v1

# Pinned to a specific version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/versions/<version>/mcp?api-version=v1
```
Set it as an environment variable in `.env` for local dev, or via `azd env set TOOLBOX_ENDPOINT "<url>"` for deployed agents.

### Running the Sample

#### Using `azd` (Recommended)

```bash
azd ai agent run
```

The agent starts on `http://localhost:8088`.

#### Using the Foundry Toolkit VS Code Extension

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Chat with a running agent using the **Agent Inspector**:

1. Start the agent locally first using **Using `azd`** or **Without `azd`** above. The agent listens on `http://localhost:8088/`.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
3. The Inspector auto-connects to the running agent. Send messages to chat with the agent and watch the streamed responses.

#### Without `azd`

```bash
cp .env.example .env  # skip if .env already exists
# Edit .env and fill in your values, then:
export $(grep -v '^#' .env | xargs)

python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
python main.py
```

The agent starts on `http://localhost:8088`.

### Testing

**Bash:**
```bash
azd ai agent invoke --local '{"message": "Search the web for Azure AI Foundry news"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"Search the web for Azure AI Foundry news\"}'
```

Or use `curl`:

```bash
# Turn 1
curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
    -H "Content-Type: application/json" \
    -d '{"message": "Search the web for Azure AI Foundry news"}'

# Turn 2 (same session)
curl -sS -N -X POST "http://localhost:8088/invocations?agent_session_id=chat-001" \
    -H "Content-Type: application/json" \
    -d '{"message": "Tell me more about the first result"}'
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
azd ai agent invoke "Search the web for Azure AI Foundry news"
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

---

## Supported Scenarios

The sample toolbox can be configured for any of these 14 scenarios. For each scenario, create a `agent.manifest.yaml` file (see examples below) and pass it to `azd ai agent init -m <manifest-file>`.

<details>
<summary><strong>View all 14 supported scenarios</strong></summary>

Refer to [`samples/python/toolbox/azd/README.md`](../../../../toolbox/azd/README.md#supported-scenarios) for complete inline documentation of all scenarios including:

1. **Web Search** — Bing web search (no auth required)
2. **File Search** — Vector store RAG search
3. **Code Interpreter** — Python code execution
4. **MCP Key-Auth (GitHub)** — GitHub MCP with PAT
5. **MCP No-Auth** — Public MCP servers
6. **MCP OAuth (Managed)** — Foundry-managed OAuth
7. **MCP OAuth (Custom)** — Bring-your-own OAuth app
8. **MCP Agent Identity** — Entra ID agent identity
9. **Azure AI Search** — Search index queries
10. **A2A (Agent-to-Agent)** — Remote agent delegation
11. **Bing Custom Search** — Scoped web search
12. **OpenAPI Key-Auth** — REST API integration
13. **MCP OAuth (Entra Passthrough)** — User identity delegation
14. **Multi-Tool Toolbox** — Web search + GitHub MCP combined

Each scenario includes a complete `agent.manifest.yaml` example with parameter definitions and resource configurations.

</details>
## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
