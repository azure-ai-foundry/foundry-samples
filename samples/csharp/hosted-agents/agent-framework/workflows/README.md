# Workflows

A multi-agent workflow that chains three translation agents into a sequential pipeline: English → French → Spanish → English.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

```bash
azd ai agent invoke --local "The quick brown fox jumps over the lazy dog"
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "The quick brown fox jumps over the lazy dog", "stream": false}'
```

Expected output: three lines showing the text in French, Spanish, then back in English.

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
