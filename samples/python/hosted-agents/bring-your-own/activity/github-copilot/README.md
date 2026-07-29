<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency note for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# What this sample demonstrates

A simple **conversational assistant** hosted agent built with the **Bring Your Own** approach on the **Activity protocol** in Python. Unlike the [echo](../echo) sample, this is a real conversational assistant: it chats through the **[GitHub Copilot SDK](https://pypi.org/project/github-copilot-sdk/)** — which runs its own model + tool-calling loop — and exposes a couple of custom tools (a to-do list and reading files shared in the chat).

It demonstrates how [`azure-ai-agentserver-activity`](https://pypi.org/project/azure-ai-agentserver-activity/) acts as the Foundry host — bridging to the [M365 Agents SDK](https://github.com/microsoft/Agents-for-python) for activity processing and outbound Teams delivery — while the **Copilot SDK** does the AI heavy lifting. You don't hand-write a tool-calling loop: you point the SDK at a model, register a few tools, and call `send_and_wait`.

## How It Works

The message handler is tiny. It forwards the user's text to the Copilot SDK and sends back the reply:

```python
from azure.ai.agentserver.activity import ActivityAgentServerHost
import client as copilot_client

host = ActivityAgentServerHost()  # simple Teams agent model (default)
app = host.agent_app

@app.activity("message")
async def on_message(context, _state):
    conversation_id = context.activity.conversation.id
    text = (context.activity.text or "").strip()
    reply = await copilot_client.ask(conversation_id, text)
    await context.send_activity(reply)

host.run()
```

Everything AI-related lives in three small modules alongside `main.py`:

| File | Responsibility |
|------|----------------|
| [`client.py`](src/github-copilot-activity/client.py) | The Copilot SDK harness. Creates one `CopilotClient` session per container, wires your **Foundry model** as the provider, registers the tools, and exposes `ask(conversation_id, text) -> str`. |
| [`tools.py`](src/github-copilot-activity/tools.py) | The custom tool set defined with `copilot.define_tool` (JSON schema generated from Pydantic models): a per-conversation to-do list (add / list / complete). |
| [`files.py`](src/github-copilot-activity/files.py) | When the user shares a file, downloads it and extracts text (plain text, PDF, DOCX, PPTX) which `main.py` folds straight into the prompt so the model can reason over it. |

### The Copilot SDK runs the agent loop

The key difference from a raw-LLM sample: **you don't write the tool-calling loop.** The Copilot SDK's `session.send_and_wait(text)` internally calls the model, invokes any tools the model requests, feeds the results back, re-prompts the model, and repeats until it produces a final answer. The agent just registers tools with `define_tool` and reads the reply.

```python
# client.py (abridged)
session = await client.create_session(
    session_id=os.environ.get("FOUNDRY_AGENT_SESSION_ID") or str(uuid.uuid4()),
    provider=ProviderConfig(
        type="azure",
        base_url=os.environ["FOUNDRY_PROJECT_ENDPOINT"],
        wire_api="responses",
        bearer_token=DefaultAzureCredential().get_token("https://ai.azure.com/.default").token,
    ),
    model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
    tools=tools.build_tools(conversation_id),
    system_message={"mode": "replace", "content": SYSTEM_MESSAGE},
    on_permission_request=PermissionHandler.approve_all,
)
event = await session.send_and_wait(text, timeout=90)
reply = event.to_dict()["data"]["content"]
```

### Model auth (your Foundry model via Managed Identity)

The assistant uses a **Foundry model deployment** (`gpt-5-mini` by default). The Copilot SDK talks to it over the **Responses** wire API using a bearer token minted from `DefaultAzureCredential` — the hosted agent's built-in Managed Identity model access, so there's **no GitHub token** and **no extra per-agent RBAC grant** required. The provider is pointed at the platform-injected `FOUNDRY_PROJECT_ENDPOINT`, and the deployment name comes from `AZURE_AI_MODEL_DEPLOYMENT_NAME` (both set for you at deploy time from [azure.yaml](azure.yaml)).

### Session model

The Foundry platform pins each Teams conversation to its own container and injects a valid `FOUNDRY_AGENT_SESSION_ID`. The assistant uses that as the Copilot SDK session id (one session per container), falling back to a random UUID only for local runs.

### Agent Hosting

The agent runs on the [Azure AI AgentServer Activity SDK](https://pypi.org/project/azure-ai-agentserver-activity/), which exposes a REST API endpoint that speaks the Azure AI Activity protocol (`POST /activity/messages`), plus platform headers, session resolution, OpenTelemetry tracing, and health probes.

### Agent Deployment

You build and ship the agent to Microsoft Foundry with the [Azure Developer CLI](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd) using the **native** `azure.ai.agent` flow. The [azure.yaml](azure.yaml) is checked in (Foundry provider) and declares a `gpt-5-mini` model deployment, so `azd provision` stands up the platform (Foundry project + Container Registry + model), and `azd deploy` builds the image, creates the agent version, and the azd foundry extension registers the Azure Bot + Microsoft Teams channel for you.

## Prerequisites

Make sure the following are installed and available:

| Requirement | Why you need it |
|-------------|-----------------|
| [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) | Provisions infrastructure and deploys the agent. Use **1.25 or later**. Install the agent service target with `azd extension install azure.ai.agents`. |
| [Azure CLI (`az`)](https://learn.microsoft.com/cli/azure/install-azure-cli) | Authentication (`az login`). |
| [Python 3.10+](https://www.python.org/downloads/) | The agent runtime (built inside the Docker image; also handy for local edits). |
| [Docker](https://www.docker.com/products/docker-desktop/) | **Optional.** `azd deploy` uses a remote ACR build, so you don't need Docker unless you want to build the image locally. |
| **Model quota** | A `gpt-5-mini` (GlobalStandard) deployment is created by `azd provision`. Make sure your subscription has capacity in the target region. |

### Required permissions

For the default flow (provision + deploy + sideload into your own Teams):

- **Owner** (or **Contributor + User Access Administrator**) on the target subscription — the deploy creates role assignments.
- **Azure AI User** or **Cognitive Services User** at the subscription or resource-group scope.

Only needed for the **org-wide** path (publishing to your tenant app catalog), not the default sideload:

- **Teams admin** (`AppCatalog.ReadWrite.All`) to publish to the org app catalog.
- **Tenant admin** to approve a tenant-wide configuration.

> [!IMPORTANT]
> **Pick a unique agent name before your first deploy.** The agent name becomes the GLOBAL
> Azure Bot name (`<agent-name>-bot-uai`), which must be unique. If you keep the default
> `github-copilot-activity`, the bot-creation step can fail with `"The requested bot name is not available"`
> because someone (or a previous deploy) already claimed it. Pick a short, lowercase name and
> set it as the service key and `name:` in [azure.yaml](azure.yaml) (keep the two in sync).

> [!NOTE]
> **Region availability:** This sample relies on [Foundry hosted agents](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd), so your Foundry account and related resources must live in a region that supports them (and has `gpt-5-mini` capacity). See the [hosted-agent region list](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd) for the current set.

## Local Debug in VS Code

### Step 1: Open the correct workspace folder

The launch configuration and tasks are defined inside `src/github-copilot/`, not the repo root. VS Code must have that folder loaded as a workspace root before F5 works.

If you opened the repo root folder, add the inner folder first:

1. **File → Add Folder to Workspace…**
2. Select `src/github-copilot/` and click **Add**.

The Explorer panel should now show `github-copilot` as a workspace root (with its own `.vscode/` visible). You can then save this as a multi-root workspace file if you like.

### Step 2: Prepare the LLM model and .env file

Sign in to the Azure account that owns your Foundry project. The agent uses `DefaultAzureCredential` to acquire auth tokens at runtime, and Azure CLI credentials are one of the credential sources it checks — so `az login` is required for local runs:

```powershell
az login
```

Copy `.env.example` to `.env` and fill in the required values:

```powershell
Copy-Item src/github-copilot-activity/.env.example src/github-copilot-activity/.env
```

Then open `.env` and set:

| Variable | Description |
|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | Your Foundry project endpoint, e.g. `https://<account>.services.ai.azure.com/api/projects/<project>` |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | The model deployment name, e.g. `gpt-5-mini` |

### Step 3: Press F5

Press **F5** (or **Run → Start Debugging**). The launch configuration will:

1. Install `agentsplayground` if not already installed (one-time, via winget).
2. Create a `.venv` (Python 3.13) and install `requirements.txt` if not already done.
3. Start the agent (`main.py`) under the VS Code debugger.
4. Launch **M365 Agents Playground** automatically once port 8088 is ready.

When the debug session ends, the `postDebugTask` kills `agentsplayground` and any process still bound to port 8088.

Once the Playground window opens, type a message — the agent echoes it back. You can set breakpoints in `main.py` as with any Python project.

## Deploying the Agent to Microsoft Foundry

### Step 1: Sign in

```powershell
# Install the azd extension that provides `host: azure.ai.agent` (one-time)
azd extension install azure.ai.agents

# Sign in to both CLIs
az login
azd auth login

# Initialize the azd environment for this agent
azd ai agent init
```

### Step 2: Provision and deploy

```bash
azd provision   # Foundry project + Container Registry + gpt-5-mini deployment
azd deploy      # remote ACR build → agent version → Bot + Teams channel (postdeploy)
```

`azd deploy` runs a remote ACR build, pushes the image, creates the **agent version** from the
checked-in [azure.yaml](azure.yaml), and patches the `activity` protocol onto the agent endpoint.
The azd foundry extension's postdeploy then registers the **Azure Bot** (instance identity) and
the **Microsoft Teams** channel.

### Step 3: Chat with it in Teams

Package and install the Teams app using the generated `TEAMS_APP_SETUP.md` guide, then open
**Microsoft Teams**, find your agent in the chat list (or under **Apps → Manage your apps**),
and start chatting. Try:

- "Hi, what can you do?"
- "Add a task: buy coffee" → "List my tasks" → "Mark it done"
- Share a PDF/DOCX and ask "What are the key points?"

> [!NOTE]
> If **Upload a custom app** is greyed out, custom-app upload is disabled for your account —
> ask an admin to enable it (Teams Admin Center → **Teams apps** → **Setup policies**), or
> publish the package to your organization's app catalog for tenant-wide availability.

## Related samples

- [echo](../echo) — the minimal Activity-protocol "hello world" (no model).
