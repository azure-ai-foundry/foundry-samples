# Local Tools

A hotel search assistant with local C# function tools — demonstrates how to define tools that the LLM can invoke, a key advantage of code-based hosted agents over prompt agents.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Find hotels in Seattle for Dec 20-25 under $200/night", "stream": false}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
