# What this sample demonstrates

A [LangGraph](https://langchain-ai.github.io/langgraph/) **human-in-the-loop** agent hosted on Foundry over the **Responses protocol** using [`langchain_azure_ai.agents.hosting`](https://github.com/langchain-ai/langchain-azure/tree/main/libs/azure-ai/langchain_azure_ai/agents/hosting). The agent drafts a proposal for the user's task, pauses for human review via [`langgraph.types.interrupt`](https://langchain-ai.github.io/langgraph/concepts/human_in_the_loop/), and supports three decisions — **approve**, **revise** (with feedback), or **reject** — surfaced to the client as the standard OpenAI `mcp_approval_request` output item plus a paired `function_call` channel for rich resume payloads.

## How It Works

### Graph shape

```
START → draft → await_approval ──approve──► END (emit final draft)
                       │
                       └──revise(feedback)──► draft (loop)
                       │
                       └──reject──► host returns response.failed (interrupt_rejected)
```

The graph state declares three channels:

- **`messages`** — the channel the Responses host emits to the client. Only the **final approved draft** is appended to it.
- **`draft`** — the current proposal text under review.
- **`revision_history`** — list of `{draft, feedback}` pairs from prior revision rounds; each round seeds the next `draft` call with the rejected draft and the reviewer's feedback.

Routing out of `await_approval` is done by returning a [`Command(goto=...)`](https://langchain-ai.github.io/langgraph/how-tos/command/) instead of static edges, so the same node can finalize, loop, or stay paused depending on the resume value.

State is persisted by an `InMemorySaver` checkpointer keyed by the `conversation.id` from the Responses request, so follow-up requests continue the paused run. See [main.py](main.py) for the full implementation.

> **Production note.** `InMemorySaver` keeps the checkpoint in process memory only — a container restart loses paused runs. Production HITL agents should swap in a durable checkpointer (Cosmos DB, Redis, or a Foundry-managed store).

### Review decisions

The Responses host emits two paired output items for each pause, both keyed by the same interrupt id:

* an `mcp_approval_request` item (`server_label == "langgraph"`, `arguments` JSON contains `{"interrupt_id": "<id>", "value": {"draft": "<text>"}}`) — the OpenAI-standard approval channel, and
* a `function_call` item with `name == "__hosted_agent_adapter_interrupt__"` — a parallel rich channel for callers that want to send arbitrary resume payloads via `function_call_output`.

| Decision | Client sends | Resume value seen by `await_approval` | Outcome |
|---|---|---|---|
| **Approve** | `mcp_approval_response` with `approve: true` and `approval_request_id: "<id>"` | `{"draft": "<original draft>"}` (proposed dict echoed; no `feedback`) | Final draft appended to `messages`; turn ends with `response.completed`. |
| **Reject** | `mcp_approval_response` with `approve: false` (optionally `reason: "<text>"`) | _(node never re-enters)_ | Host returns `response.failed` with `code="interrupt_rejected"`. Checkpoint preserved; client can retry. |
| **Revise** | `function_call_output` with `call_id: "<id>"` and `output: '{"resume": {"feedback": "<text>"}}'` | `{"feedback": "<text>"}` | Graph loops back to `draft` with `revision_history` appended; a new draft is generated and re-paused for review. |

### Agent Hosting

The compiled graph is hosted with `ResponsesHostServer`, which exposes the OpenAI-compatible Responses endpoint at `/responses` and handles conversation history, interrupt serialization, and streaming lifecycle events automatically.

## Running the Agent Host

Follow the instructions in the [Running the Agent Host Locally](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md#running-the-agent-host-locally) section of the README in the parent directory to run the agent host.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/langgraph/README.md) for more details. Use this README for sample queries you can send to the agent.

### Step 1 — Submit the task

Send a POST request with a `conversation.id` so the checkpoint can be matched on subsequent requests:

```bash
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "Draft a marketing email for our new AI product launch.", "conversation": {"id": "demo-hitl-1"}}'
```

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"input": "Draft a marketing email for our new AI product launch.", "conversation": {"id": "demo-hitl-1"}}').Content
```

The response `output` will contain an `mcp_approval_request` whose `arguments` JSON describes the proposed draft:

```json
{"interrupt_id": "<id>", "value": {"draft": "Subject: ...\n\nHi ..."}}
```

Note the `id` field of the `mcp_approval_request` item — you will reference it as `approval_request_id` (or as the `call_id` of the paired `function_call`) in the next request.

### Step 2 — Send a decision

**Approve** — the host resumes the graph and emits the final draft:

```bash
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"conversation": {"id": "demo-hitl-1"}, "input": [{"type": "mcp_approval_response", "approval_request_id": "<id>", "approve": true}]}'
```

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"conversation": {"id": "demo-hitl-1"}, "input": [{"type": "mcp_approval_response", "approval_request_id": "<id>", "approve": true}]}').Content
```

**Reject** — the turn ends with `response.failed` `code="interrupt_rejected"`; the pending interrupt remains in the checkpoint:

```bash
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"conversation": {"id": "demo-hitl-1"}, "input": [{"type": "mcp_approval_response", "approval_request_id": "<id>", "approve": false, "reason": "tone is too casual"}]}'
```

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"conversation": {"id": "demo-hitl-1"}, "input": [{"type": "mcp_approval_response", "approval_request_id": "<id>", "approve": false, "reason": "tone is too casual"}]}').Content
```

**Revise** — target the paired `function_call` item (its `call_id` is the same interrupt id) and send the feedback:

```bash
curl -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"conversation": {"id": "demo-hitl-1"}, "input": [{"type": "function_call_output", "call_id": "<id>", "output": "{\"resume\": {\"feedback\": \"Shorter and more energetic, add a clear call to action.\"}}"}]}'
```

```powershell
(Invoke-WebRequest -Uri http://localhost:8088/responses -Method POST -ContentType "application/json" -Body '{"conversation": {"id": "demo-hitl-1"}, "input": [{"type": "function_call_output", "call_id": "<id>", "output": "{\"resume\": {\"feedback\": \"Shorter and more energetic, add a clear call to action.\"}}"}]}').Content
```

The graph generates a new draft incorporating the feedback and pauses again with a fresh `mcp_approval_request` — repeat Step 2 until you approve or reject.

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```
Draft a marketing email for our new AI product launch.
```

When the agent pauses with an approval request, the Inspector renders an interactive approval card. Approve, reject, or send revision feedback directly from there.

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
