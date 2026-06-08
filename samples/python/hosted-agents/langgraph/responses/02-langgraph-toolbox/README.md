# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) ReAct agent wired to a **Foundry Toolbox** in Microsoft Foundry, hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting). The toolbox exposes `web_search` plus a **connection-backed GitHub Copilot MCP** tool — the toolbox owns the GitHub PAT (declared once on the connection) and the agent calls the MCP tools without ever seeing the secret.

> Unlike sample 04-mcp (which connects directly to GitHub MCP from the agent process using a runtime `GITHUB_PAT` env var), this sample lets **the toolbox** own the credential. The PAT is stored once on the Foundry connection, the toolbox is granted access to it, and the agent calls the toolbox endpoint with its own managed identity.

## Prerequisites

1. A [GitHub Personal Access Token](https://github.com/settings/tokens) with the scopes needed by the GitHub MCP tools you want the agent to call (e.g. `repo`, `read:user`).
2. Provide the token to `azd` so it can populate the `github-mcp-conn` connection at provision time:

   ```powershell
   azd env set GITHUB_PAT "<your-github-pat>"
   ```

   `azd ai agent init` will also prompt for this when it encounters the `github_pat` parameter in [agent.manifest.yaml](agent.manifest.yaml).

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, the toolbox declared in [`agent.manifest.yaml`](agent.manifest.yaml) (named `agent-tools` with `web_search` and the GitHub MCP connection) is created automatically.

## How It Works

### Toolbox tool loading

[`langchain_azure_ai.tools.AzureAIProjectToolbox`](https://github.com/langchain-ai/langchain-azure/blob/main/libs/azure-ai/langchain_azure_ai/tools/_toolbox.py) opens an MCP session against the toolbox endpoint, authenticates with `DefaultAzureCredential`, injects the required `Foundry-Features` header, sanitizes tool schemas, and returns standard LangChain `BaseTool` instances. Tools are loaded **lazily** (once, on the first request) and reused for all subsequent turns; each tool invocation opens its own short-lived MCP session.

### LangGraph Agent

The compiled graph is built with `langchain.agents.create_agent(model, tools=tools, system_prompt=...)`, which returns a compiled LangGraph runnable implementing the standard ReAct loop. The system prompt instructs the agent to ground answers in tool-provided sources and include a brief Sources section when URLs are present.

### OAuth consent handling

When the toolbox's MCP connection needs additional consent that isn't already covered by the PAT, the Foundry MCP gateway raises an MCP error with code `-32006` and a URL on `consent.azure-apim.net`. The sample installs a `handle_tool_error` callback on every loaded tool that detects this case and returns a friendly tool message containing the consent URL — the agent surfaces it to the caller, and the conversation continues instead of crashing.

### Tool schema sanitization

Some MCP servers return tools with malformed JSON schemas (e.g. `object`-type schemas with no `properties` field), which the OpenAI tool format rejects. The sample patches missing or empty `properties` before handing the tools to LangGraph.

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, streaming lifecycle events, and tool-call surfacing automatically.

See [main.py](main.py) for the full implementation.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host. This sample additionally requires `GITHUB_PAT` in the azd environment so `azd provision` can populate the GitHub MCP connection.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing an `input` field:

```bash
# Tool discovery
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "What tools do you have?"}'

# web_search
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "What is the latest stable Python release?"}'

# GitHub MCP
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "List my 5 most recently updated GitHub repos."}'
```

```powershell
# Tool discovery
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What tools do you have?"}').Content

# web_search
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What is the latest stable Python release?"}').Content

# GitHub MCP
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "List my 5 most recently updated GitHub repos."}').Content
```

Invoke with `azd`:

```powershell
azd ai agent invoke --local "List my 5 most recently updated GitHub repos."
```

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
List my 5 most recently updated GitHub repos.
```

## Deploying the Agent to Foundry

Make sure `GITHUB_PAT` is set in the azd environment (see [Prerequisites](#prerequisites)), then follow the instructions in the [Deploying the Agent to Foundry](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

### Deploying with the Foundry Toolkit VS Code Extension

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

## Troubleshooting

### Agent starts but returns no tools

Check that the toolbox exists in your Foundry project and that `TOOLBOX_NAME` matches its name. `agent-tools` is the default name provisioned by `azd up` against [agent.manifest.yaml](agent.manifest.yaml).

### OAuth consent required

If the toolbox's GitHub MCP connection needs additional consent, the gateway returns an MCP error with code `-32006` and a URL on `consent.azure-apim.net`. The agent surfaces this URL through a tool message of the form:

```
OAuth consent required. Open this URL in a browser to authorize the toolbox connection, then retry the request: https://consent.azure-apim.net/...
```

Open the URL, complete the consent flow, then retry the original request.

### Tool call failures don't crash the agent

A `handle_tool_error` callback is installed on every loaded tool, so MCP tool errors are returned as tool messages rather than raising exceptions that would break conversation state.

### Tool schemas rejected by OpenAI

The sample sanitizes malformed schemas from MCP servers (missing `properties` on `object`-type schemas) at load time. If you see `400 Invalid tool schema` errors anyway, inspect the raw tool schema returned by your MCP server — there may be additional shape issues.

### Wrong PAT or insufficient scopes

If GitHub MCP tools return `401 Unauthorized` or "Resource not accessible by integration", confirm `GITHUB_PAT` has the scopes the tool needs (typically `repo`, `read:user`). Re-run `azd env set GITHUB_PAT "<new-token>"` and `azd provision` to update the connection.
