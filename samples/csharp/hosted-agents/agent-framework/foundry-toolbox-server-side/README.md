# Foundry Toolbox — Server-Side Tools

An agent that loads a Foundry Toolbox and passes its tools to the agent as **server-side tools**. The Foundry platform handles tool discovery and invocation through the Responses API — the agent process does not connect to the toolbox MCP proxy or invoke tools locally.

`GetToolboxToolsAsync()` fetches the tool definitions from the configured toolbox and they are then passed to `AsAIAgent(..., tools: ...)`. At runtime the Foundry platform invokes those tools server-side on the agent's behalf, so the agent container only needs the control-plane call to fetch the definitions — it does not broker MCP connections.

## Creating a Foundry Toolbox

You can create a Foundry Toolbox by code. Refer to this sample for an example: [Foundry Toolbox CRUD Sample](https://github.com/Azure/azure-sdk-for-python/blob/main/sdk/ai/azure-ai-projects/samples/hosted_agents/sample_toolboxes_crud.py).

You can also create a Foundry Toolbox in the Foundry portal. Read more about it [in the Foundry toolbox documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox).

> If you set up a project with this sample and provision the resources using `azd provision`, a Foundry Toolbox will be created with the tools declared in [`agent.manifest.yaml`](agent.manifest.yaml) — by default, `code_interpreter` (server-side code execution) plus an `mcp` tool pointing at the public `https://gitmcp.io/Azure/azure-rest-api-specs` MCP server. Swap either out for any other toolbox tool type that fits your scenario.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What tools do you have?", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Find the latest API version for Microsoft.CognitiveServices accounts in the azure-rest-api-specs repo.", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Use the code interpreter to compute the 30th Fibonacci number.", "stream": false}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
