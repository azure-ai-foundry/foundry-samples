# HelloWorld

A minimal "hello world" hosted agent using the [Agent Framework](https://github.com/microsoft/agent-framework) with the Responses protocol in C#. This is the recommended starting point for understanding how agents are hosted on Foundry.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a request to the agent:

```bash
azd ai agent invoke --local "What is Microsoft Foundry?"
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "What is Microsoft Foundry?", "stream": false}'
```

The server will respond with a JSON object containing the response text and a response ID. You can use this response ID to continue the conversation in subsequent requests.

### Multi-turn conversation

To have a multi-turn conversation with the agent, include the previous response id in the request body:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "Can you summarize that?", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID", "stream": false}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
