# Invocations Echo Agent

A minimal echo agent hosted as a Foundry Hosted Agent using the **Invocations protocol** and the [Agent Framework](https://github.com/microsoft/agent-framework). The agent reads the request body as plain text, passes it through a custom `EchoAIAgent`, and writes the echoed text back in the response. No LLM or Azure credentials are required.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](../README.md#running-the-agent-host-locally) section of the parent README to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing a "message" field to interact with the agent:

**Bash:**
```bash
azd ai agent invoke --local '{"message": "Hello, world!"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"Hello, world!\"}'
```

Or use `curl`:

```bash
curl -X POST http://localhost:8088/invocations -i -H "Content-Type: application/json" -d '{"message": "Hello, world!"}'
```

The server will respond with a JSON object containing the response text. The `-i` flag includes the HTTP response headers in the output, which includes the session ID that can be used for multi-turn conversations. Here is an example of the response:

```
HTTP/1.1 200
content-type: application/json
x-agent-invocation-id: ec04d020-a0e7-441e-ae83-db75635a9f83
x-agent-session-id: 9370b9d4-cd13-4436-a57f-03b843ac0e17
x-platform-server: azure-ai-agentserver-core/2.0.0 (dotnet/10.0)

{"response":"Echo: Hello, world!"}
```

### Multi-turn conversation

To have a multi-turn conversation with the agent, take the session ID from the response headers of the previous request and include it in URL parameters for the next request:

```bash
curl -X POST "http://localhost:8088/invocations?agent_session_id=9370b9d4-cd13-4436-a57f-03b843ac0e17" -i -H "Content-Type: application/json" -d '{"message": "How are you?"}'
```

## Deploying the Agent to Foundry

To deploy the agent to Foundry, follow the instructions in the [Deploying the Agent to Foundry](../README.md#deploying-the-agent-to-foundry) section of the parent README.
