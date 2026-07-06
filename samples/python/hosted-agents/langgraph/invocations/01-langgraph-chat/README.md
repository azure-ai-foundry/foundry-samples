# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) multi-turn chat agent with two local tools, hosted on Foundry over the **Invocations protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting).

Unlike the Responses protocol, the Invocations protocol does **not** surface intermediate tool calls — clients receive only the final assistant text (or its token deltas when streaming).

## How It Works

### LangGraph Agent

The agent is built with `langchain.agents.create_agent(model, tools=[...], checkpointer=MemorySaver())`, which returns a compiled LangGraph runnable implementing the standard ReAct loop. The agent registers two local tools:

- `get_current_time` — returns the current UTC time.
- `calculator` — evaluates a simple math expression.

See [main.py](src/langgraph-chat-invocations/main.py) for the full implementation.

### Agent Hosting

The compiled graph is hosted with `InvocationsHostServer`, which exposes the Invocations endpoint at `/invocations` and supports both non-streaming (single JSON response) and streaming (`{"stream": true}` SSE token deltas) modes.

Multi-turn continuity is provided by the LangGraph `MemorySaver` checkpointer: the host wires the resolved `agent_session_id` into `RunnableConfig.configurable.thread_id`, so each session's history is preserved across turns. Replace `MemorySaver` with a durable checkpointer (Redis, Cosmos DB, etc.) for production.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request with a JSON body containing a `"message"` field. The `-i` flag includes the response headers, which carry the `x-agent-session-id` header you need for multi-turn conversations:

```bash
# Turn 1 — triggers the get_current_time tool
curl -i -X POST http://localhost:8088/invocations \
    -H "Content-Type: application/json" \
    -d '{"message": "What time is it right now?"}'
```

```powershell
# Turn 1 — triggers the get_current_time tool (response object exposes both Headers and Content)
Invoke-WebRequest -Uri http://localhost:8088/invocations -Method POST -ContentType "application/json" -Body '{"message": "What time is it right now?"}'
```

Example response:

```
HTTP/1.1 200
content-type: application/json
x-agent-session-id: 9370b9d4-cd13-4436-a57f-03b843ac0e17

{"response": "The current UTC time is ..."}
```

Take the `x-agent-session-id` from the previous response and pass it as a URL parameter on the next requests to continue the conversation:

```bash
# Turn 2 — triggers the calculator tool
curl -X POST 'http://localhost:8088/invocations?agent_session_id=REPLACE_WITH_SESSION_ID' \
    -H "Content-Type: application/json" \
    -d '{"message": "What is 42 * 17?"}'

# Turn 3 — context recall (no tool call)
curl -X POST 'http://localhost:8088/invocations?agent_session_id=REPLACE_WITH_SESSION_ID' \
    -H "Content-Type: application/json" \
    -d '{"message": "Add 100 to that result."}'
```

```powershell
# Turn 2 — triggers the calculator tool
(Invoke-WebRequest -Uri "http://localhost:8088/invocations?agent_session_id=REPLACE_WITH_SESSION_ID" -Method POST -ContentType "application/json" -Body '{"message": "What is 42 * 17?"}').Content

# Turn 3 — context recall (no tool call)
(Invoke-WebRequest -Uri "http://localhost:8088/invocations?agent_session_id=REPLACE_WITH_SESSION_ID" -Method POST -ContentType "application/json" -Body '{"message": "Add 100 to that result."}').Content
```

### Streaming

Add `"stream": true` to receive per-token text deltas as SSE `data:` lines, followed by `event: done`:

```bash
curl -N -X POST http://localhost:8088/invocations \
    -H "Content-Type: application/json" \
    -d '{"message": "Count to 5.", "stream": true}'
```

```powershell
# Note: Invoke-WebRequest buffers the full response; the SSE deltas are visible in .Content but not delivered incrementally.
(Invoke-WebRequest -Uri http://localhost:8088/invocations -Method POST -ContentType "application/json" -Body '{"message": "Count to 5.", "stream": true}').Content
```

Invoke with `azd`:

```powershell
azd ai agent invoke --local '{"message": "What time is it right now?"}'
```

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
What time is it right now?
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

### Deploying with the Foundry Toolkit VS Code Extension

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a tab-based **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate what it can.
2. If prompted, complete **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one) to deploy to.
3. On the **Basics** tab, configure the core deployment settings:
   - **Deployment Method**: **Code** (upload as a ZIP) or **Container** (Docker image via ACR).
   - For **Code**, pick a packaging option: **Remote** or **Local**.
   - For **Container**, pick a registry option: default ACR, your own ACR, or a prebuilt ACR image.
   - **Hosted Agent Name**: confirm the name to register with the hosting service.
4. On the **Review + Deploy** tab, finalize the runtime and resources:
   - Confirm the auto-detected runtime details (language, entry point, or Dockerfile).
   - Pick a **CPU and Memory** size.
   - Click **Deploy**. Fields are validated inline, and the extension handles the build/upload, agent version creation, and RBAC role assignment.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.
