# Text Search RAG

A support specialist agent with Retrieval Augmented Generation (RAG) — demonstrates how to ground agent answers in external knowledge using `TextSearchProvider`.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What is your return policy?", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "How long does shipping take?", "stream": false}'
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "How do I clean my tent?", "stream": false}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
