# Foundry Toolbox — Server-Side Tools

An agent that consumes a Foundry Toolbox as **server-side tools**. The Agent Framework hosting layer connects to the toolbox's managed MCP proxy at startup, discovers its tools, and injects them into every request. Tool calls are brokered by the Foundry platform's toolbox proxy, so the agent never hard-codes or locally executes the tools.

`AddFoundryToolboxes(toolboxName)` registers the toolbox with the hosting layer. At startup the hosting layer connects to the toolbox's managed MCP proxy (derived from `FOUNDRY_PROJECT_ENDPOINT`), lists its tools, and caches them. Every incoming request then has those tools injected automatically, and the Foundry platform executes the tool calls through the proxy. The agent itself declares no toolbox tools.

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, a Foundry Toolbox will be created with the tools declared in [`agent.manifest.yaml`](agent.manifest.yaml) — by default, `code_interpreter` (server-side code execution) plus an `mcp` tool pointing at the public `https://gitmcp.io/Azure/azure-rest-api-specs` MCP server. Swap either out for any other toolbox tool type that fits your scenario.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell), `azd`, or the **Agent Inspector** in the Foundry Toolkit VS Code extension. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What tools do you have?", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Find the latest API version for Microsoft.CognitiveServices accounts in the azure-rest-api-specs repo.", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Use the code interpreter to compute the 30th Fibonacci number.", "stream": false}'
```

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
What tools do you have?
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.

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
