# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) multi-turn chat agent with two local tools, hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting).

## How It Works

### LangGraph Agent

The agent is built with `langchain.agents.create_agent(model, tools=[...])`, which returns a compiled LangGraph runnable implementing the standard ReAct loop (call model → if tool calls were requested, run them → loop back → return the final message). The agent registers two local tools:

- `get_current_time` — returns the current UTC time.
- `calculator` — evaluates a simple math expression.

See [main.py](main.py) for the full implementation.

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, streaming lifecycle events, and tool-call surfacing automatically.

Conversation state is managed server-side by the platform via `previous_response_id` — there is no application-side session storage.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

Send a POST request to the server with a JSON body containing an `input` field:

```bash
# Turn 1 — triggers the get_current_time tool
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "What time is it right now?"}'
```

```powershell
# Turn 1 — triggers the get_current_time tool
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What time is it right now?"}').Content
```

The server responds with a JSON object containing the response text and a response ID. Use that response ID to continue the conversation:

```bash
# Turn 2 — chain via previous_response_id; triggers the calculator tool
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "What is 42 * 17?", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}'

# Turn 3 — context recall (no tool call)
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "Add 100 to that result.", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}'
```

```powershell
# Turn 2 — chain via previous_response_id; triggers the calculator tool
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "What is 42 * 17?", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}').Content

# Turn 3 — context recall (no tool call)
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "Add 100 to that result.", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}').Content
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
