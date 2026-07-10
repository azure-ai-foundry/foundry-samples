# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) ReAct agent wired to a **Foundry Toolbox** in Microsoft Foundry, hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting). The toolbox exposes `web_search` plus the public **Microsoft Learn MCP** server behind one endpoint — the agent calls the toolbox tools without managing any credentials.

## Prerequisites

- Python 3.12+
- A Microsoft Foundry project
- Azure CLI installed and logged in (`az login`)

The sample bundles a [`toolbox.yaml`](src/toolbox-langgraph/toolbox.yaml) that defines the tools. Neither tool requires a secret, so there's nothing extra to configure before you provision.

## Creating a Foundry Toolbox

The sample bundles a [`toolbox.yaml`](src/toolbox-langgraph/toolbox.yaml) that defines `web_search` plus the public Microsoft Learn MCP server (no authentication). Create the toolbox once from that file:

```bash
azd ai toolbox create my-toolbox --from-file ./toolbox.yaml
```

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, the toolbox declared in [`azure.yaml`](azure.yaml) (named `my-toolbox` with `web_search` and the Microsoft Learn MCP server) is created automatically.

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

See [main.py](src/toolbox-langgraph/main.py) for the full implementation.

## Option 1: Azure Developer CLI (`azd`)

### Prerequisites

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
2. Install the Foundry extension:

   ```bash
   azd ext install microsoft.foundry
   ```

3. Authenticate:

   ```bash
   azd auth login
   ```

### Initialize the agent project

No cloning required. Create a new folder and initialize from the manifest:

```bash
mkdir hosted-langgraph-agent && cd hosted-langgraph-agent
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/responses/02-langgraph-toolbox/azure.yaml
```

### Provision Azure resources (if needed)

If you don't already have a Foundry project, model deployment, and toolbox, provision them. `azd provision` creates the toolbox (`my-toolbox` by default) referenced by `TOOLBOX_NAME`:

```bash
azd provision
```

### Run the agent locally

```bash
azd ai agent run
```

The agent host will start on `http://localhost:8088`.

### Invoke the local agent

In a separate terminal, send a query to the agent:

```bash
azd ai agent invoke --local "How do I create a Foundry project with the Azure CLI?"
```

Or invoke directly with curl:

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

### Deploy to Foundry

Deploy the agent to Microsoft Foundry:

```bash
azd deploy
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

### Invoke the deployed agent

```bash
azd ai agent invoke "How do I create a Foundry project with the Azure CLI?"
```

## Option 2: VS Code (Foundry Toolkit)

### Prerequisites

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-windows-ai-studio.windows-ai-studio)** extension installed.
2. For debugging Python in VS Code, install the **[Python](https://marketplace.visualstudio.com/items?itemName=ms-python.python)** extension pack.

### Set up the Python virtual environment

- Open the Command Palette (`Ctrl+Shift+P`) and run **Python: Create Environment...** to create a virtual environment in the workspace (or **Python: Select Interpreter** to use an existing one).
- Ensure `pip` is version 26.1 or newer (check with `pip --version`). Older versions fail to resolve this sample's dependencies. Upgrade if needed:

  ```bash
  python -m pip install --upgrade pip
  ```

- Install dependencies in the virtual environment. One transitive dependency ships as a pre-release, so pre-releases must be allowed when using `uv`:

  ```bash
  # use uv to accelerate
  pip install uv
  uv pip install --prerelease=allow -r requirements.txt

  # or pure pip
  pip install -r requirements.txt
  ```

### Run and debug the agent

Press **F5** to start the agent. The agent starts and the **Agent Inspector** opens automatically. Chat with the agent in the Inspector.

### Or run manually, then open the Inspector

1. Set the required environment variables (including `TOOLBOX_NAME`), and sign in to Azure with the Azure CLI (`az login`).
2. Start the agent: `python main.py` (listens on `http://localhost:8088`).
3. Command Palette (`Ctrl+Shift+P`) → **Foundry Toolkit: Open Agent Inspector**, then send a message to test.

### Deploy to Foundry

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate settings.
2. If prompted, complete **Foundry Project Setup** to select subscription and project.
3. On the **Basics** tab, choose deployment method (**Code** or **Container**) and confirm the agent name.
4. On **Review + Deploy**, confirm runtime details, pick **CPU and Memory** size, and click **Deploy**.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

## Troubleshooting

### Agent starts but returns no tools

Check that the toolbox exists in your Foundry project and that `TOOLBOX_NAME` matches its name. `my-toolbox` is the default name provisioned by `azd up` against [azure.yaml](azure.yaml).

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
