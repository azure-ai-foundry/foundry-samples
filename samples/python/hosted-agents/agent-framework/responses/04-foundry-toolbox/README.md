# Agent with Foundry Toolbox (Responses Protocol)

An [Agent Framework](https://github.com/microsoft/agent-framework) agent that uses **Foundry Toolbox** for tool discovery, hosted on Microsoft Foundry using the **Responses protocol**. Foundry Toolbox is a managed tool registry in Microsoft Foundry that lets you define tools centrally and share them across agents.

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, a Foundry Toolbox will be created with the specified tools in [`agent.manifest.yaml`](agent.manifest.yaml).

### Authentication Methods

You can connect to MCP servers in Foundry Toolbox that use different authentication methods. This sample demonstrates the following authentication methods:

- **No authentication**: The tool does not require any authentication. The agent can invoke the tool without providing any credentials. Sample MCP server: `https://gitmcp.io/Azure/azure-rest-api-specs`
- **Key-based authentication**: The tool requires a key to authenticate. Sample MCP server: `https://api.githubcopilot.com/mcp` (GitHub MCP server) with a Personal Access Token (PAT) for authentication.
- **OAuth2 authentication (managed)**: The tool requires OAuth2 to authenticate. Sample MCP server: `https://api.githubcopilot.com/mcp` (GitHub MCP server) with OAuth2 for authentication.
- **Agent identity authentication**: The tool requires an agent identity token to authenticate. Sample MCP server: `https://{foundry-resource-name}.cognitiveservices.azure.com/language/mcp?api-version=2025-11-15-preview` (Azure Language MCP server) with agent identity for authentication.
- **Entra Pass-through authentication**: The tool requires an Entra pass-through token to authenticate. Sample MCP server: Microsoft Outlook MCP server with Entra pass-through for authentication.

> Definitions of these authentication methods can be found in the [agent.manifest.yaml](agent.manifest.yaml) file in this sample. The GitHub MCP connection defaults to using a PAT for authentication in this sample, but you can switch to OAuth2 by changing the `project_connection_id` field in the `agent.manifest.yaml` file and following the instructions in the comments.

There are also Non-MCP tools in the toolbox that support different authentication methods. Learn more at the [Foundry sample repository](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/SUPPORTED_TOOLBOX_SCENARIOS.md).

## How it works

### Model Integration

The agent uses `FoundryChatClient` from the Agent Framework to create an OpenAI-compatible Responses client. It connects to the toolbox's MCP endpoint via `MCPStreamableHTTPTool`, which discovers and invokes the toolbox's tools over MCP at runtime. The endpoint URL is provided through the `FOUNDRY_TOOLBOX_ENDPOINT` environment variable.

See [main.py](main.py) for the full implementation.

## Running the agent

### Option 1: Azure Developer CLI (`azd`)

#### Prerequisites

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (1.25 or later)
2. Install the unified Foundry CLI extension bundle (provides `azd ai agent`, `connection`, `inspector`, `project`, `routine`, `skill`, and `toolbox`):
   ```bash
   # If you previously installed individual extensions, uninstall them first:
   #   azd ext uninstall azure.ai.agents
   #   azd ext uninstall azure.ai.toolboxes
   azd ext install microsoft.foundry
   ```
3. Authenticate:
   ```bash
   azd auth login
   ```

#### Create the toolbox with `azd ai`

> [!TIP]
> If you use GitHub Copilot for Azure to scaffold a hosted agent that consumes this toolbox, the following skill references describe the same endpoint contract (env var, headers, MCP protocol, citation patterns, and troubleshooting) that the agent must implement:
>
> - [Toolbox reference](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/microsoft-foundry/foundry-agent/create/references/toolbox-reference.md) — endpoint format, MCP protocol, OAuth consent handling, citation patterns, and troubleshooting.
> - [Use toolbox in a hosted agent](https://github.com/microsoft/GitHub-Copilot-for-Azure/blob/main/plugin/skills/microsoft-foundry/foundry-agent/create/references/use-toolbox-in-hosted-agent.md) — endpoint resolution, env-var contract, payload shape, code integration patterns, and tracing.

This sample's agent reads a `TOOLBOX_ENDPOINT` URL at startup. `azd ai agent init` + `azd provision` will create the toolbox declared in [`agent.manifest.yaml`](agent.manifest.yaml) automatically. If you prefer to create the toolbox directly with `azd` (for reuse across agents or to manage versions out-of-band), use the unified `microsoft.foundry` extension:

1. Point `azd` at your Foundry project (once per shell):

   ```bash
   export PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
   azd ai project set $PROJECT_ENDPOINT
   ```

2. (Connections.) The tools used in this sample — `web_search` and `code_interpreter` — are built-in and do not require project connections, so this step is skipped here. For a connection-backed example (MCP servers with API keys, OAuth, etc.), see the [`langgraph-toolbox`](../../../bring-your-own/responses/langgraph-toolbox/README.md) sample.

3. Author a `toolbox.yaml` describing the tools:

   ```yaml
   # toolbox.yaml
   description: Web search + code interpreter for the agent-framework Foundry-toolbox sample
   tools:
     - type: web_search
       name: web_search
     - type: code_interpreter
       name: code_interpreter
       container:
         type: auto
   ```

4. Create the toolbox from that file:

   ```bash
   azd ai toolbox create agent-tools --from-file ./toolbox.yaml
   ```

   The first version becomes the default automatically. Use `azd ai toolbox list`, `azd ai toolbox show agent-tools`, and `azd ai toolbox version list agent-tools` to inspect, and `azd ai toolbox delete agent-tools --force` to remove it.

   To stage incremental changes safely, use `azd ai toolbox connection add/remove` and `azd ai toolbox skill add/list/remove` &mdash; each creates a new toolbox version that carries forward existing connections and skills but **doesn't** change the default. Promote a version with `azd ai toolbox publish agent-tools <version>` when you're ready to make it active.

5. Retrieve the MCP endpoint and pass it to the agent. The agent uses `client.get_toolbox("agent-tools")`, which resolves through `TOOLBOX_ENDPOINT`:

   ```bash
   azd ai toolbox show agent-tools --output json    # returns the MCP endpoint URL
   azd env set TOOLBOX_ENDPOINT "https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/agent-tools/mcp?api-version=v1"
   ```

#### Initialize the agent project

No cloning required. Create a new folder and initialize from the manifest:

```bash
mkdir my-toolbox-agent && cd my-toolbox-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/04-foundry-toolbox/agent.manifest.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` will guide you through creating one.

#### Provision Azure resources (if needed)

If you don't already have a Foundry project and model deployment:

```bash
azd provision
```

> Running `azd provision` for this sample will also create a Foundry Toolbox with the tools specified in [`agent.manifest.yaml`](agent.manifest.yaml).

#### Run the agent locally

```bash
azd ai agent run
```

The agent host will start on `http://localhost:8088`.

#### Invoke the local agent

In a separate terminal, from the project directory:

```bash
azd ai agent invoke --local "What tools do you have?"
```

#### Deploy to Foundry

Once tested locally, deploy to Microsoft Foundry:

```bash
azd deploy
```

For the full deployment guide, see [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

#### Invoke the deployed agent

```bash
azd ai agent invoke "What tools do you have?"
```

### Option 2: VS Code (Foundry Toolkit)

#### Prerequisites

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.azure-ai-foundry)** extension installed.
2. Sign in to Azure in VS Code.

#### Create the project

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Create Hosted Agent**.
2. Select this sample from the gallery. The extension scaffolds the project into a new workspace and generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically.
3. Complete the **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one).

#### Run and debug the agent

Press **F5** to start the agent in debug mode. The agent host will start on `http://localhost:8088`.

#### Test with Agent Inspector

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
2. The Inspector connects to the running agent. Send messages to chat and view streamed responses.

#### Deploy to Foundry

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate settings.
2. If prompted, complete **Foundry Project Setup** to select subscription and project.
3. On the **Basics** tab, choose deployment method (**Code** or **Container**) and confirm the agent name.
4. On **Review + Deploy**, confirm runtime details, pick **CPU and Memory** size, and click **Deploy**.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

### Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

## Next steps

- [Quickstart: Create a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent) — end-to-end walkthrough using `azd`
- [Tool catalog](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/tool-catalog) — browse available tools to extend your agent (Bing Search, Azure AI Search, file search, code interpreter, and more)
- [Manage hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/manage-hosted-agent) — monitor and manage deployed agents
- [Basic agent](../01-basic/) — minimal agent with no tools
- [Add local tools](../02-tools/) — sample with locally-defined Python tool functions
- [Build multi-agent workflows](../05-workflows/) — sample with chained agent pipelines
