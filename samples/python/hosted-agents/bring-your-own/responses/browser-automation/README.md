# Browser Automation Agent (Python, Responses Protocol)

A **Bring Your Own** hosted agent that automates web browsers using
[Playwright CLI](https://github.com/microsoft/playwright-cli) via Azure AI Foundry
Toolbox MCP. Built with `azure-ai-agentserver-responses` (no Agent Framework).

> This agent helps you with handling multiple sessions parellely.
Use this agent when you need the control over the streamed response, whatever you need to see in the UI or outputs, need a hands on proper raw SDKs.
There is a simple sample available available at agent-framework/responses/14-browser-automation-agent se that for the common needs.

## What This Sample Demonstrates

- **Lazy browser session creation** — no session is created until the model actually needs one
- **Multi-session support** — run multiple concurrent browsers for parallel tasks
- **Toolbox MCP integration** — uses Foundry Toolbox to provision remote Chromium browsers
- **Skills system** — guided workflows loaded on demand (form-filling, web scraping)
- **Kill session priority** — immediately honours user requests to close sessions

## Folder Structure

```
browser-automation/
├── main.py              # Responses handler, session management, agentic tool loop
├── toolbox.py           # MCP client for Foundry Toolbox (browser session lifecycle)
├── browser.py           # playwright-cli subprocess wrapper (BrowserSession class)
├── skills.py            # Skill markdown loader
├── constants.py         # System prompt, tool definitions, config constants
├── skills/
│   ├── form-filler.md   # Form-filling workflow with date picker handling
│   └── web-scraper.md   # Data extraction with pagination support
├── agent.yaml           # Hosted agent deployment config
├── agent.manifest.yaml  # Agent init manifest for azd tooling
├── Dockerfile           # Container build
├── requirements.txt     # Python dependencies
└── README.md
```

## How It Works

```text
User → Responses Protocol → Handler (main.py)
                                ↓
                          Model (tool-calling loop)
                                ↓
                    ┌───────────┴───────────┐
                    ↓                       ↓
           run_browser(cmd)         create_session(name)
                    ↓                       ↓
           playwright-cli           Toolbox MCP (toolbox.py)
           (browser.py)             create_browser_session()
                    ↓                       ↓
           Remote Chromium ←── CDP URL ────┘
```

1. User sends a message → handler passes it to the model with tool definitions.
2. Model calls `run_browser(command="goto", args=["https://..."])`.
3. On **first call**, a "default" session is lazily created:
   - `toolbox.py` calls Toolbox MCP `create_session` → gets `cdp_url` + `live_view_url`
   - `browser.py` runs `playwright-cli attach --cdp=<url>` to connect
4. Subsequent `run_browser` calls reuse the existing session.
5. For parallel work, the model calls `create_session(name)` for additional browsers.
6. `kill_session` closes sessions immediately when requested.

## Prerequisites

- An Azure AI Foundry project with a deployed chat model (e.g., `gpt-4.1`).
- A Foundry Toolbox with the `browser_automation_preview ` tool configured (backed by an Azure Playwright workspace).
- Azure CLI installed and authenticated (`az login`).
- Python 3.12+ for local development.

## Configuration

### Required Environment Variables

| Variable | Description |
|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Foundry project endpoint (auto-injected in hosted containers) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Model deployment name (e.g., `gpt-4.1`) |

### Optional Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `BROWSER_TIMEOUT_SECONDS` | `180` | Timeout for each playwright-cli command |

## Running Locally

```bash
# Set environment
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"

# Install dependencies
pip install -r requirements.txt
npm install -g @playwright/cli@latest

# Run
python src/main.py
```

### Invoke the agent

```bash
curl -sS -X POST http://localhost:8088/responses \
    -H "Content-Type: application/json" \
    -d '{"input": "Go to https://example.com and tell me the page title"}' | jq .
```

### Test in Agent Inspector

Open **Agent Inspector** in VS Code (Command Palette → **Foundry Toolkit: Open Agent Inspector**) to interactively send messages.

## Deploying to Foundry

```bash
# Initialize from manifest
azd ai agent init -m agent.manifest.yaml

# Set Playwright workspace connection values
azd env set PLAYWRIGHT_SERVICE_URL "wss://<region>.api.playwright.microsoft.com/playwrightworkspaces/<workspace-id>/browsers"
azd env set PLAYWRIGHT_SERVICE_RESOURCE_ID "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.LoadTestService/playwrightWorkspaces/<workspace-name>"
azd env set PLAYWRIGHT_SERVICE_ACCESS_TOKEN "<playwright-workspace-access-token>"

# Deploy
azd deploy
```

`PLAYWRIGHT_SERVICE_ACCESS_TOKEN` is used as a secret parameter for the Playwright workspace project connection.

## Tools Available to the Model

| Tool | Description |
|------|-------------|
| `run_browser` | Run a playwright-cli command (session auto-created on first use) |
| `create_session` | Create an additional named browser session for parallel work |
| `kill_session` | Close a session immediately (or `"all"` to close all) |
| `run_parallel` | Execute commands across sessions concurrently |
| `list_sessions` | Show all active sessions with live view URLs |
| `load_skill` | Load a skill for guided workflow instructions |

## Multi-Session Examples

The agent supports multiple concurrent browser sessions, enabling parallel workflows and human-in-the-loop scenarios.

### Example 1: Parallel research across tabs

```
User: "Compare pricing on aws.amazon.com and azure.microsoft.com side by side"

Agent:
  → create_session("aws")
  → create_session("azure")
  → run_parallel([
      {session: "aws", command: "goto", args: ["https://aws.amazon.com/pricing/"]},
      {session: "azure", command: "goto", args: ["https://azure.microsoft.com/pricing/"]}
    ])
  → run_parallel([
      {session: "aws", command: "snapshot"},
      {session: "azure", command: "snapshot"}
    ])
  → Responds with comparison
```

### Example 2: Form filling with OTP (human-in-the-loop)

```
User: "Sign up on example.com/register with my email test@contoso.com"

Agent:
  → run_browser(command: "goto", args: ["https://example.com/register"])
  → run_browser(command: "fill", args: ["#email", "test@contoso.com"])
  → run_browser(command: "click", args: ["#send-otp"])
  → Responds: "OTP sent to test@contoso.com. Please provide the code when you receive it."

User: "The OTP is 847291"

Agent:
  → run_browser(command: "fill", args: ["#otp-input", "847291"])
  → run_browser(command: "click", args: ["#verify-btn"])
  → run_browser(command: "snapshot")
  → Responds: "Verified! Proceeding with registration..."
```

### Example 3: Monitor one page while working on another

```
User: "Fill the job application on careers.contoso.com, and keep checking my email
       on mail.contoso.com for a confirmation"

Agent:
  → create_session("application")
  → create_session("email")
  → run_browser(session: "application", command: "goto", args: ["https://careers.contoso.com/apply"])
  → ... fills the form in "application" session ...
  → run_browser(session: "email", command: "goto", args: ["https://mail.contoso.com"])
  → run_browser(session: "email", command: "snapshot")
  → Responds: "Form submitted. Checking email — no confirmation yet. I'll check again."

User: "Check email again"

Agent:
  → run_browser(session: "email", command: "reload")
  → run_browser(session: "email", command: "snapshot")
  → Responds: "Confirmation email received!"
```

### Example 4: Kill sessions when done

```
User: "Close the email browser, keep the application one"

Agent:
  → kill_session("email")
  → Responds: "Closed email session. Application session still active."
```

## Skills

Skills are markdown instruction files in `skills/`:

- **form-filler** — Step-by-step form filling with date picker handling, multi-page navigation
- **web-scraper** — Data extraction using JavaScript eval, pagination handling

The model loads skills on demand via the `load_skill` tool when it needs guided instructions for a specific workflow type.

## Customization

- Edit `constants.py` to modify the system prompt or tool definitions.
- Add new skills as `.md` files in `skills/`.
- Modify `browser.py` to add/restrict allowed playwright-cli commands.
- Adjust `agent.yaml` resources (`cpu`, `memory`) for heavier workloads.
