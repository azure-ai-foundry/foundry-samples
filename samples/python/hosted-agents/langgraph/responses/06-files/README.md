# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) agent that **manipulates files** using three local filesystem tools (`get_cwd`, `list_files`, `read_file`) and the **`code_interpreter`** tool loaded from a [Foundry Toolbox](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox) via [`langchain_azure_ai.tools.AzureAIProjectToolbox`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/tools). Hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting).

In hosted mode the platform mounts files uploaded to a hosted agent session into the agent's working directory, so the same local tools work against user-provided files. The bundled `resources/contoso_q1_2026_report.txt` ships inside the container image so the demo flow works without uploading anything.

## How It Works

### Tools

| Tool | Source |
|---|---|
| `get_cwd` | Local `@tool` — returns the agent's current working directory. |
| `list_files` | Local `@tool` — lists entries under a directory. |
| `read_file` | Local `@tool` — returns the contents of a UTF-8 text file. |
| `code_interpreter` | Foundry Toolbox — runs Python in a managed sandbox for math/data work. |

System prompt: *"You are a friendly assistant. Keep your answers brief. Make sure all mathematical calculations are performed using the code interpreter instead of mental arithmetic."*

### LangGraph Agent

The compiled graph is built with `langchain.agents.create_agent(model, tools=[...], system_prompt=...)`, which returns a compiled LangGraph runnable implementing the standard ReAct loop (call model → if tool calls were requested, run them → loop back → return the final message).

See [main.py](src/langgraph-files-responses/main.py) for the full implementation.

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, streaming lifecycle events, and tool-call surfacing automatically.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host. This sample additionally requires `TOOLBOX_NAME` to point at a Foundry Toolbox that exposes the `code_interpreter` tool.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

Ask the agent to discover and analyze the bundled quarterly report:

```bash
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "Find the quarterly report under `{cwd}/resources` and tell me the difference of revenue between q1 2026 and q1 2025."}'
```

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "Find the quarterly report under `{cwd}/resources` and tell me the difference of revenue between q1 2026 and q1 2025."}').Content
```

Invoke with `azd`:

```powershell
azd ai agent invoke --local "Find the quarterly report under \`{cwd}/resources\` and tell me the difference of revenue between q1 2026 and q1 2025."
```

The agent will call `get_cwd` and `list_files` to locate the file, `read_file` to load its contents, and `code_interpreter` to compute the revenue delta.

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
Find the quarterly report under `{cwd}/resources` and tell me the difference of revenue between q1 2026 and q1 2025.
```

## Uploading files to a hosted session

After deploying the agent to Foundry, uploaded session files are mounted into the agent's working directory, where the same local tools can read them. Upload a file to the current session with:

```bash
azd ai agent files upload -f resources/contoso_q1_2026_report.txt
```

Then ask the agent about it:

```bash
azd ai agent invoke "Read the quarterly report I just uploaded and summarize the year-over-year revenue change."
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

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
