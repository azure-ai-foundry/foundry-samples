# MCP Tools

An agent demonstrating two layers of MCP (Model Context Protocol) tool integration: client-side MCP (agent connects directly to an MCP server) and server-side MCP (LLM provider connects to the MCP server on behalf of the agent).

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
azd ai agent invoke --local "Search Microsoft Learn for how to use dependency injection in ASP.NET Core"
```

Or use `curl`:

```bash
# Triggers client-side MCP tools (docs search, code samples, docs fetch)
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Search Microsoft Learn for how to use dependency injection in ASP.NET Core", "stream": false}'

# Triggers code sample search (client-side only)
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Find a C# code sample for creating an Azure Blob Storage container", "stream": false}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
