# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) agent that uses **Foundry Toolbox** for tool discovery and hosted using the **Responses protocol**. Foundry Toolbox is a managed tool registry in Microsoft Foundry that lets you define tools centrally and share them across agents.

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, a Foundry Toolbox will be created with the specified tools in [`agent.manifest.yaml`](agent.manifest.yaml).

## How It Works

### Model Integration

The agent uses `FoundryChatClient` from the Agent Framework to create an OpenAI-compatible Responses client. It connects to the toolbox's MCP endpoint via `MCPStreamableHTTPTool`, which discovers and invokes the toolbox's tools over MCP at runtime.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../../README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What tools do you have?"}'
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](../../README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

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
