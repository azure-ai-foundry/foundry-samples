# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) agent whose tools are loaded from a **remote MCP server** via [`langchain-mcp-adapters`](https://github.com/langchain-ai/langchain-mcp-adapters), hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting). By default the agent connects to the [GitHub Copilot MCP server](https://api.githubcopilot.com/mcp/), but `MCP_SERVER_URL` can point at any HTTP-transport MCP endpoint.

## How It Works

### MCP tool loading

[`langchain_mcp_adapters.client.MultiServerMCPClient`](https://github.com/langchain-ai/langchain-mcp-adapters) opens a streamable-HTTP session against the configured MCP server, forwards an `Authorization: Bearer <token>` header sourced from `GITHUB_PAT`, and returns standard LangChain `BaseTool` instances. The sample loads tools once at startup with `asyncio.run(...)`; each tool invocation opens its own short-lived MCP session.

### LangGraph Agent

The compiled graph is built with `langchain.agents.create_agent(model, tools=tools)`, which returns a compiled LangGraph runnable implementing the standard ReAct loop (call model → if tool calls were requested, run them → loop back → return the final message). No system prompt is set — tool descriptions from the MCP server drive selection.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, streaming lifecycle events, and tool-call surfacing automatically.

## Prerequisites

1. A [GitHub Personal Access Token](https://github.com/settings/tokens) with the scopes needed by the GitHub MCP tools you want the agent to call (e.g. `repo`, `read:user`).
2. Provide the token to the agent. Pick the path that matches how you will run it:
   - **`python main.py`** — set `GITHUB_PAT` in `.env` (see [.env.example](.env.example)).
   - **`azd ai agent run` (local) or `azd deploy` (cloud)** — set it in the azd environment so `azd` can resolve the `${GITHUB_PAT}` placeholder in [agent.yaml](agent.yaml). Your shell's `export` / `$env:` values are not propagated to azd:

     ```powershell
     azd env set GITHUB_PAT "<your-github-pat>"
     ```

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host. This sample additionally requires `GITHUB_PAT` (and optionally `MCP_SERVER_URL` if you target a different MCP server).

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

Ask the agent a question that exercises one of the GitHub MCP tools:

```bash
# List repositories
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "List my 5 most recently updated GitHub repos."}'

# Search issues
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "Search GitHub issues mentioning langchain-azure."}'
```

```powershell
# List repositories
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "List my 5 most recently updated GitHub repos."}').Content

# Search issues
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "Search GitHub issues mentioning langchain-azure."}').Content
```

Invoke with `azd`:

```powershell
azd ai agent invoke --local "List my 5 most recently updated GitHub repos."
```

Intermediate `function_call` / `function_call_output` items are surfaced for every MCP tool the agent invokes — same shape as the local-tools sample, but the tool execution happens inside the remote MCP server.

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
List my 5 most recently updated GitHub repos.
```

## Targeting a different MCP server

Set `MCP_SERVER_URL` to any HTTP-transport MCP endpoint and adjust `GITHUB_PAT` (or the `Authorization` header logic in [main.py](main.py)) to match its auth scheme.

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
