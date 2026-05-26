# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) hosted browser automation agent using **Foundry Toolbox** and the **Browser Automation tool** (Azure Playwright Service), hosted using the **Responses protocol**. The agent connects to a remote Chromium browser via Foundry Toolbox and runs Playwright CLI commands against it for general browsing, web scraping, and form filling.

## How It Works

### Solution Overview

When a user asks for browser work, the agent:

1. Connects to a Foundry Toolbox MCP endpoint in the same Foundry project.
2. Calls `create_session` from that Toolbox to provision a remote Chromium browser via Azure Playwright Service.
3. Connects Playwright CLI to the returned CDP WebSocket URL.
4. Uses `run_playwright_cli` to invoke Playwright CLI commands against the remote browser.
5. Calls `close_browser_session` to detach Playwright CLI state and end the remote browser when done.

```text
User
  -> Foundry hosted agent
      -> Agent Framework tools
          -> Foundry Toolbox MCP create_session
              -> Azure Playwright Service remote Chromium
          -> Playwright CLI
              -> remote browser CDP session
```

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with the `ResponsesHostServer`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol.

### Prompt-Guided Behavior

The agent reads a single base prompt from `prompts/base.md`. That prompt contains the browser lifecycle, safety, web extraction, and form-filling guidance used at runtime.

See [main.py](src/browser_automation_agent_sample_foundry/main.py) for the full implementation and [docs/sample-structure.md](docs/sample-structure.md) for the design rationale.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/browser_automation_agent_sample_foundry/` | Shared Python implementation for hosting, settings, prompts, tools, and agent construction. |
| `prompts/base.md` | Browser lifecycle, safety, cleanup, web extraction, and form-filling rules. |
| `skills/azure-playwright-browser-automation/SKILL.md` | Playwright CLI operational reference for remote Azure Playwright Service sessions. |
| `docs/sample-structure.md` | Design notes explaining the sample structure and extension points. |

## Prerequisites

- An Azure AI Foundry project with a deployed chat model (e.g., `gpt-4.1`).
- An Azure Playwright workspace. If you do not have one, follow [Create a workspace](https://learn.microsoft.com/azure/app-testing/playwright-workspaces/quickstart-run-end-to-end-tests?tabs=playwrightcli&pivots=playwright-test-runner#create-a-workspace).
- Azure CLI installed and authenticated (`az login`).
- Docker, if you want to build the container locally.
- Python 3.11 or later and `uv` (or `pip`) for local development.

For hosted-agent setup, see [Deploy hosted agents with azd](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd).

## Configuration

### Runtime environment variables

Copy `.env.example` to `.env` for local development, or set these values in your shell:

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
```

Or in PowerShell:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
```

### Optional environment variables

| Variable | Default | Purpose |
| --- | --- | --- |
| `BROWSER_AGENT_PLAYWRIGHT_CLI_TIMEOUT_SECONDS` | `180` | Optional timeout for each Playwright CLI command. |
| `BROWSER_AGENT_MCP_TIMEOUT_SECONDS` | `120` | Optional timeout for Toolbox MCP calls. |

The Toolbox endpoint is resolved as `<FOUNDRY_PROJECT_ENDPOINT>/toolboxes/browser-automation-tools/mcp?api-version=v1` and authenticated with the hosted agent identity.

### Provisioning parameters

`PLAYWRIGHT_SERVICE_URL` and `PLAYWRIGHT_SERVICE_RESOURCE_ID` are not runtime environment variables. They are `azd` provisioning parameters used by [`agent.manifest.yaml`](agent.manifest.yaml) to create a `PlaywrightWorkspace` project connection with `AgenticIdentityToken` authentication and the `browser-automation-tools` toolbox wired to that connection. Set them with `azd env set` before running `azd provision` in the [deployment steps](#deploying-the-agent-to-foundry).

## Running the Agent Host

This sample uses a package-style `src/` layout with `pyproject.toml`, `uv.lock`, and `uv sync` instead of the simpler flat `main.py` plus `requirements.txt` pattern used by smaller samples. Use the sample-specific `uv` setup below for local development.

### Local setup with `uv`

Install dependencies and run the hosted-agent server locally:

```bash
uv sync --prerelease allow
npm install -g @playwright/cli@latest
playwright-cli install --skills
uv run browser-automation-agent-sample-foundry
```

The extra structure keeps the browser lifecycle tools, prompt instructions, and support modules isolated while preserving the same hosted-agent entry point. If you prefer `pip` for local development, install the package with `pip install -e .`.

## Interacting with the agent

> Depending on how you run the agent host, you can invoke the agent using `curl` (`Invoke-WebRequest` in PowerShell) or `azd`. Please refer to the [parent README](../../README.md) for more details.

Send a POST request to the server with a JSON body containing an `"input"` field:

```bash
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "Open https://example.com and report the page title."}'
```

Or in PowerShell:

```powershell
(Invoke-WebRequest `
  -Uri http://localhost:8088/responses `
  -Method POST `
  -ContentType "application/json" `
  -Body '{"input": "Open https://example.com and report the page title."}').Content
```

With `azd`:

```bash
azd ai agent invoke --local --new-session "Open https://example.com and report the page title."
```

The server returns a response ID that you can use to continue the same conversation and reuse the browser session in later requests:

```bash
curl -X POST http://localhost:8088/responses \
  -H "Content-Type: application/json" \
  -d '{"input": "Now take a screenshot of the page.", "previous_response_id": "REPLACE_WITH_PREVIOUS_RESPONSE_ID"}'
```

### Test in Agent Inspector

Once the agent is running locally, open **Agent Inspector** in VS Code (Command Palette: **Foundry Toolkit: Open Agent Inspector**) to interactively send messages and view responses.

Type the following message in Inspector:

```text
Open https://example.com and report the page title.
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](../../README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

When deploying, set `PLAYWRIGHT_SERVICE_URL` and `PLAYWRIGHT_SERVICE_RESOURCE_ID` in your `azd` environment to point to your Azure Playwright workspace:

```bash
azd env set PLAYWRIGHT_SERVICE_URL "wss://<region>.api.playwright.microsoft.com/playwrightworkspaces/<workspace-id>/browsers"
azd env set PLAYWRIGHT_SERVICE_RESOURCE_ID "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.LoadTestService/playwrightWorkspaces/<workspace-name>"
```

If these are not set, running `azd ai agent init -m <agent.manifest.yaml>` will prompt you to enter them interactively.

Run `azd provision` before `azd deploy`. Provisioning uses [`agent.manifest.yaml`](agent.manifest.yaml) to create the Foundry project connection to your Playwright workspace and the `browser-automation-tools` toolbox:

```bash
azd provision
```

The deployed hosted agent identity needs Foundry access at runtime to call the model and authenticate against the Toolbox MCP endpoint. The deployment tooling handles standard hosted-agent RBAC assignments when your account has sufficient permissions.

The Playwright workspace connection uses the project-level Foundry Agent Identity. After provisioning the project, grant that identity **Playwright Workspace Contributor** on the Playwright workspace:

```bash
AI_PROJECT_ID=$(azd env get-value AZURE_AI_PROJECT_ID)
PLAYWRIGHT_SCOPE=$(azd env get-value PLAYWRIGHT_SERVICE_RESOURCE_ID)
PRINCIPAL_ID=$(az resource show \
  --ids "$AI_PROJECT_ID" \
  --api-version 2025-04-01-preview \
  --query properties.agentIdentity.agentIdentityId \
  -o tsv)

az role assignment create \
  --assignee-object-id "$PRINCIPAL_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "78cf819f-0969-4ebe-8759-015c6efcd5bf" \
  --scope "$PLAYWRIGHT_SCOPE"
```

Or in PowerShell:

```powershell
$AiProjectId = azd env get-value AZURE_AI_PROJECT_ID
$PlaywrightScope = azd env get-value PLAYWRIGHT_SERVICE_RESOURCE_ID
$PrincipalId = az resource show `
  --ids $AiProjectId `
  --api-version 2025-04-01-preview `
  --query properties.agentIdentity.agentIdentityId `
  -o tsv

az role assignment create `
  --assignee-object-id $PrincipalId `
  --assignee-principal-type ServicePrincipal `
  --role "78cf819f-0969-4ebe-8759-015c6efcd5bf" `
  --scope $PlaywrightScope
```

Then deploy the hosted agent:

```bash
azd deploy
```

## Customize the sample

- Change prompt behavior in `prompts/base.md`.
- Add deeper procedural knowledge as skills under `skills/`.
- Add new tools in `src/browser_automation_agent_sample_foundry/tools.py`.

See [docs/sample-structure.md](docs/sample-structure.md) for the design rationale.

## Guidance

This sample is intended as a starting point, not a production-ready browser automation platform. Before using it in production, review authentication, network access, data handling, secret management, logging, browser permissions, and approval flows for state-changing actions.

The `run_playwright_cli` tool intentionally invokes only `playwright-cli` with a named session and optional `PLAYWRIGHT_MCP_CDP_ENDPOINT`; it does not expose general shell execution.

The default hosted container resources (`cpu: "0.25"`, `memory: "0.5Gi"`) are minimal. Increase them in `agent.yaml` for multi-step scraping, longer QA sessions, or data-heavy browser automation.

Useful references:

- [Hosted agents in Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [Agent Framework overview](https://learn.microsoft.com/en-gb/agent-framework/overview/?pivots=programming-language-python)
- [Agent Framework skills](https://learn.microsoft.com/en-gb/agent-framework/agents/skills?pivots=programming-language-python)
- [Playwright CLI](https://github.com/microsoft/playwright-cli)
