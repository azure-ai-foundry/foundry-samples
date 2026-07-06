# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) hosted agent that can be deployed to Foundry and published to Teams.

After publishing, users can send messages with file attachments to the agent. It can also answer questions related with Teams and calendar.

![Using Work IQ tool](src/teams-activity-dotnet-agent-framework/teams-activity.png)

## How It Works

### Model Integration

See [Program.cs](src/teams-activity-dotnet-agent-framework/Program.cs) for the full implementation. Work IQ tools are configured in toolbox that can be used by agent, so that it can answer questions to your Teams and calendar data.

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with `AgentHost.CreateBuilder()`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "How many meetings do I have tomorrow?"}'
```

The server will respond with a JSON object containing the response text and a response ID. You can use this response ID to continue the conversation in subsequent requests.

### Multi-turn conversation

To have a multi-turn conversation with the agent, include the previous response id in the request body. For example:

```bash
curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "How are you?", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}'
```

## Publishing the Agent

1. In the Foundry portal, click **Publish**, then choose **Publish to Teams and Microsoft 365**. For the full flow, see this [documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/publish-copilot).
2. The Foundry portal creates the Azure Bot resource and configures the messaging endpoint automatically.
3. End users need to sign in the first time they access the agent.