# Foundry Toolbox — Server-Side Tools

An agent that consumes a Foundry Toolbox as **server-side tools**. The Agent Framework hosting layer connects to the toolbox's managed MCP proxy at startup, discovers its tools, and injects them into every request. Tool calls are brokered by the Foundry platform's toolbox proxy, so the agent never hard-codes or locally executes the tools.

`AddFoundryToolboxes(toolboxName)` registers the toolbox with the hosting layer. At startup the hosting layer connects to the toolbox's managed MCP proxy (derived from `FOUNDRY_PROJECT_ENDPOINT`), lists its tools, and caches them. Every incoming request then has those tools injected automatically, and the Foundry platform executes the tool calls through the proxy. The agent itself declares no toolbox tools.

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, a Foundry Toolbox will be created with the tools declared in [`azure.yaml`](azure.yaml) — by default, `code_interpreter` (server-side code execution) plus an `mcp` tool pointing at the public `https://gitmcp.io/Azure/azure-rest-api-specs` MCP server. Swap either out for any other toolbox tool type that fits your scenario.

## Prerequisites

1. An existing Foundry project with a deployed model (or create them during setup in Option 1).
2. **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** or later.
3. **A Foundry Toolbox** exposing the server-side tools (see [Creating a Foundry Toolbox](#creating-a-foundry-toolbox) above). If declared in the sample's `azure.yaml`, `azd provision` (Option 1) creates it.

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
mkdir foundry-toolbox-agent && cd foundry-toolbox-agent
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/foundry-toolbox-server-side/azure.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` will guide you through creating one.

### Provision Azure resources (if needed)

If you don't already have a Foundry project, model deployment, and toolbox, provision them:

```bash
azd provision
```

### Run the agent locally

```bash
azd ai agent run
```

The agent host will start on `http://localhost:8088`.

### Invoke the local agent

In a separate terminal, ask the agent about its toolbox tools:

```bash
azd ai agent invoke --local "What tools do you have?"
```

Or use curl directly:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What tools do you have?", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Find the latest API version for Microsoft.CognitiveServices accounts in the azure-rest-api-specs repo.", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Use the code interpreter to compute the 30th Fibonacci number.", "stream": false}'
```

### Deploy to Foundry

Once tested locally, deploy to Microsoft Foundry:

```bash
azd deploy
```

For the full deployment guide, see [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

### Invoke the deployed agent

```bash
azd ai agent invoke "What tools do you have?"
```

## Option 2: VS Code (Foundry Toolkit)

### Prerequisites

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-windows-ai-studio.windows-ai-studio)** extension installed.
2. [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension.
3. Command Palette (`Ctrl+Shift+P`) → **C#: Check Workspace Requirements** to confirm the toolchain is ready.

### Run and debug the agent

Press **F5** to start the agent. The agent starts and the **Agent Inspector** opens automatically. Chat with the agent in the Inspector.

### Or run manually, then open the Inspector

1. Restore dependencies:

   ```bash
   dotnet restore
   ```

2. Configure the agent: copy `.env.example` to `.env` and fill in the required variables (including `TOOLBOX_NAME`). The sample loads `.env` automatically on startup.

3. Sign in to Azure with the Azure CLI so `DefaultAzureCredential` can authenticate the terminal process (the **F5** path reuses the Azure sign-in from the Foundry Toolkit, so it doesn't need a separate `az login`):

   ```bash
   az login
   ```

4. Start the agent (listens on `http://localhost:8088`):

   ```bash
   dotnet run
   ```

5. Open the Command Palette (`Ctrl+Shift+P`) → **Foundry Toolkit: Open Agent Inspector**, then send a message to test.

### Deploy to Foundry

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate settings.
2. If prompted, complete **Foundry Project Setup** to select subscription and project.
3. On the **Basics** tab, choose deployment method (**Code** or **Container**) and confirm the agent name.
4. On **Review + Deploy**, confirm runtime details, pick **CPU and Memory** size, and click **Deploy**.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.
