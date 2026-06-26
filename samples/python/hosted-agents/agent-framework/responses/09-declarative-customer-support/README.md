# What this sample demonstrates

A realistic **multi-turn** [Agent Framework](https://github.com/microsoft/agent-framework) customer-support triage agent, hosted using the **Responses protocol**. A small code-based router runs a triage agent on every user turn and dispatches to a technical or billing specialist — reading the prior conversation through the message history the hosting infrastructure replays on each turn.

> Read more about [Agent Framework agents](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/?pivots=programming-language-python) and [hosting agents behind the Responses protocol](https://learn.microsoft.com/azure/ai-foundry/agents/concepts/hosted-agents).

## How It Works

### The routing agent

[`main.py`](main.py) defines `CustomerSupportAgent`, a `BaseAgent` subclass that implements the triage flow in plain Python:

1. It runs `TriageAgent`, which looks at the full conversation so far and emits a structured `TriageResponse` (`Category`, `NeedsClarification`, `ClarificationQuestion`, `Reply`).
2. It routes on the triage decision:
   - **NeedsClarification** → asks one focused follow-up question and ends the turn.
   - **Category = "Technical"** → confirms the handoff, then streams `TechSupportAgent`'s reply directly to the caller.
   - **Category = "Billing"** → same pattern, routed to `BillingAgent`.
   - **else** → returns the triage agent's `Reply` directly (good for greetings or general questions).

Each user message re-runs the router from the top. The hosting infrastructure replays the prior turns of the conversation as the input messages, so every sub-agent call sees the full history — which is what makes the triage decision and the specialist follow-ups coherent across turns.

### Agent Hosting

[`main.py`](main.py) builds three `Agent` instances on top of a shared `FoundryChatClient` (one per role), composes them inside `CustomerSupportAgent`, and hands that agent to `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

The triage agent is configured with `response_format=TriageResponse` (a Pydantic model) so the router can read its structured fields via `result.value`. The specialist agents are plain text and stream their reply straight to the caller.

## Option 1: Azure Developer CLI (`azd`)

### Prerequisites

1. **Azure Developer CLI (`azd`)** — [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
2. Install the AI agent extension:
   ```bash
   azd ext install microsoft.foundry
   ```
3. Authenticate:
   ```bash
   azd auth login
   ```

### Initialize the agent project

No cloning required. Create a new folder and initialize from the manifest:

```bash
mkdir my-declarative-agent && cd my-declarative-agent

azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/09-declarative-customer-support/agent.manifest.yaml
```

Follow the prompts to configure your Foundry project and model deployment. If you don't have an existing Foundry project, `azd ai agent init` will guide you through creating one.

### Provision Azure resources (if needed)

If you don't already have a Foundry project and model deployment:

```bash
azd provision
```

### Run the agent locally

```bash
azd ai agent run
```

The agent host will start on `http://localhost:8088`.

### Invoke the local agent

In a separate terminal, from the project directory:

```bash
azd ai agent invoke --local "I have a problem"
```

A typical multi-turn session:

```bash
azd ai agent invoke --local "I have a problem"
# → "Could you tell me a bit more about what's going on?"

azd ai agent invoke --local "My laptop won't turn on"
# → "Connecting you with technical support..."
# → TechSupportAgent: "Let's start simple — is the charger LED on when plugged in?"

azd ai agent invoke --local "Yes the LED is on"
# → TechSupportAgent: "Great. Try a hard reset: hold the power button for 30 seconds..."
```

Or for billing:

```bash
azd ai agent invoke --local "I was double-charged this month"
# → "Connecting you with billing support..."
# → BillingAgent: "I'm sorry about that. Can you share the last 4 digits of the card on file?"
```

### Deploy to Foundry

Once tested locally, deploy to Microsoft Foundry:

```bash
azd deploy
```

For the full deployment guide, see [Deploy a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/deploy-hosted-agent).

### Invoke the deployed agent

```bash
azd ai agent invoke "I have a problem"
```

## Option 2: VS Code (Foundry Toolkit)

### Prerequisites

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.azure-ai-foundry)** extension installed.
2. Sign in to Azure in VS Code.

### Create the project

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Create Hosted Agent**.
2. Select this sample from the gallery. The extension scaffolds the project into a new workspace and generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically.
3. Complete the **Foundry Project Setup** to pick the subscription and Foundry project (or create a new one).

### Run and debug the agent

Press **F5** to start the agent in debug mode. The agent host will start on `http://localhost:8088`.

### Test with Agent Inspector

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
2. The Inspector connects to the running agent. Send messages to chat and view streamed responses.

### Deploy to Foundry

1. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Deploy Hosted Agent**. The extension opens a **Deploy Hosted Agent** wizard and reads `agent.yaml` to auto-populate settings.
2. If prompted, complete **Foundry Project Setup** to select subscription and project.
3. On the **Basics** tab, choose deployment method (**Code** or **Container**) and confirm the agent name.
4. On **Review + Deploy**, confirm runtime details, pick **CPU and Memory** size, and click **Deploy**.
5. After deployment, invoke the agent in the Agent Playground and stream live logs from the **Logs** tab.

## Next steps

- [Quickstart: Create a hosted agent](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent) — end-to-end walkthrough using `azd`
- [Manage hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/manage-hosted-agent) — monitor and manage deployed agents
