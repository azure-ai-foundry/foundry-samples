# Simple Agent

A general-purpose AI assistant — the simplest hosted agent using `AsAIAgent(model, instructions)`. This is the recommended starting point for understanding inline agent creation.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Hello! What can you help me with?", "stream": false}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
