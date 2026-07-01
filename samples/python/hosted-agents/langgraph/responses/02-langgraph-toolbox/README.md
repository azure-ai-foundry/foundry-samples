# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) ReAct agent wired to a **Foundry Toolbox** in Microsoft Foundry, hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting). The toolbox exposes `web_search` plus the public **Microsoft Learn MCP** server behind one endpoint — the agent calls the toolbox tools without managing any credentials.

## Prerequisites

- Python 3.12+
- A Microsoft Foundry project
- Azure CLI installed and logged in (`az login`)

The sample bundles a [`toolbox.yaml`](toolbox.yaml) that defines the tools. Neither tool requires a secret, so there's nothing extra to configure before you provision.

## Creating a Foundry Toolbox

The sample bundles a [`toolbox.yaml`](toolbox.yaml) that defines `web_search` plus the public Microsoft Learn MCP server (no authentication). Create the toolbox once from that file:

```bash
azd ai toolbox create my-toolbox --from-file ./toolbox.yaml
```

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, the toolbox declared in [`agent.manifest.yaml`](agent.manifest.yaml) (named `my-toolbox` with `web_search` and the Microsoft Learn MCP server) is created automatically.

> [!NOTE]
> This sample identifies the toolbox by name (`TOOLBOX_NAME`) and always consumes its current default version. The `AzureAIProjectToolbox` helper builds the MCP endpoint from the toolbox name, so it can't pin the agent to a specific toolbox version. When you publish a new default version, the agent picks it up automatically.

## How It Works

### Toolbox tool loading

[`langchain_azure_ai.tools.AzureAIProjectToolbox`](https://github.com/langchain-ai/langchain-azure/blob/main/libs/azure-ai/langchain_azure_ai/tools/_toolbox.py) opens an MCP session against the toolbox endpoint, authenticates with `DefaultAzureCredential`, sanitizes tool schemas, and returns standard LangChain `BaseTool` instances. Tools are loaded **lazily** (once, on the first request) and reused for all subsequent turns; each tool invocation opens its own short-lived MCP session.

### LangGraph Agent

The compiled graph is built with `langchain.agents.create_agent(model, tools=tools, system_prompt=...)`, which returns a compiled LangGraph runnable implementing the standard ReAct loop. The system prompt instructs the agent to ground answers in tool-provided sources and include a brief Sources section when URLs are present.

### OAuth consent handling

If a toolbox connection ever needs additional consent (for example, an MCP server backed by an OAuth connection), the Foundry MCP gateway raises an MCP error with code `-32006` and a URL on `consent.azure-apim.net`. The sample installs a `handle_tool_error` callback on every loaded tool that detects this case and returns a friendly tool message containing the consent URL — the agent surfaces it to the caller, and the conversation continues instead of crashing.

### Tool schema sanitization

Some MCP servers return tools with malformed JSON schemas (e.g. `object`-type schemas with no `properties` field), which the OpenAI tool format rejects. The sample patches missing or empty `properties` before handing the tools to LangGraph.

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, streaming lifecycle events, and tool-call surfacing automatically.

See [main.py](main.py) for the full implementation.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host.

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

# Microsoft Learn MCP
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "How do I create a Foundry project with the Azure CLI?"}'
```

```powershell
# Tool discovery
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What tools do you have?"}').Content

# web_search
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What is the latest stable Python release?"}').Content

# Microsoft Learn MCP
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "How do I create a Foundry project with the Azure CLI?"}').Content
```

Invoke with `azd`:

```powershell
azd ai agent invoke --local "How do I create a Foundry project with the Azure CLI?"
```

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
How do I create a Foundry project with the Azure CLI?
```

## Deploying the Agent to Foundry

Follow the instructions in the [Deploying the Agent to Foundry](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

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

Check that the toolbox exists in your Foundry project and that `TOOLBOX_NAME` matches its name. `my-toolbox` is the default name provisioned by `azd up` against [agent.manifest.yaml](agent.manifest.yaml).

### OAuth consent required

If a toolbox connection needs additional consent, the gateway returns an MCP error with code `-32006` and a URL on `consent.azure-apim.net`. The agent surfaces this URL through a tool message of the form:

```
OAuth consent required. Open this URL in a browser to authorize the toolbox connection, then retry the request: https://consent.azure-apim.net/...
```

Open the URL, complete the consent flow, then retry the original request.

### Tool call failures don't crash the agent

A `handle_tool_error` callback is installed on every loaded tool, so MCP tool errors are returned as tool messages rather than raising exceptions that would break conversation state.

### Tool schemas rejected by OpenAI

The sample sanitizes malformed schemas from MCP servers (missing `properties` on `object`-type schemas) at load time. If you see `400 Invalid tool schema` errors anyway, inspect the raw tool schema returned by your MCP server — there may be additional shape issues.
