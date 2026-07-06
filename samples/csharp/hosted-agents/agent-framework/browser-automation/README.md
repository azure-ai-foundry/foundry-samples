# What this sample demonstrates

An [Agent Framework](https://github.com/microsoft/agent-framework) hosted browser automation agent using **Foundry Toolbox** and the **Browser Automation tool** (Azure Playwright Service), hosted using the **Responses protocol**. The agent connects to a remote Chromium browser via Foundry Toolbox and runs Playwright CLI commands against it for general browsing, web scraping, and form filling.

## How It Works

### Solution Overview

When a user asks for browser work, the agent:

1. On startup, connects to a Foundry Toolbox MCP endpoint via `AddFoundryToolboxes` (automatic tool discovery).
2. The model calls `create_session` from the Toolbox to provision a remote Chromium browser via Azure Playwright Service.
3. Function invocation middleware intercepts the `create_session` result, stores the CDP URL and live view URL server-side (the model never sees the raw URLs).
4. Streaming middleware injects the live view URL into the SSE response so the user can watch the browser in real time.
5. Uses `run_playwright_cli` to invoke Playwright CLI commands against the remote browser.
6. Calls `close_browser_session` to detach Playwright CLI state and end the remote browser when done.

```text
User
  -> Foundry hosted agent
      -> Agent Framework (AddFoundryToolboxes)
          -> Foundry Toolbox MCP create_session
              -> Azure Playwright Service remote Chromium
      -> Middleware pipeline
          -> Function invocation: intercepts create_session, stores URLs server-side
          -> Streaming: injects live_view_url into SSE response
      -> Local tools (run_playwright_cli, close_browser_session, get_live_view_url)
          -> Playwright CLI -> remote browser CDP session
```

### Agent Hosting

The agent is hosted using the [Agent Framework](https://github.com/microsoft/agent-framework) with `AddFoundryResponses` and `AddFoundryToolboxes`, which provisions a REST API endpoint compatible with the OpenAI Responses protocol and automatically discovers toolbox tools via MCP.

### Prompt-Guided Behavior

The agent reads a single base prompt from `prompts/base.md`. That prompt contains the browser lifecycle, safety, web extraction, and form-filling guidance used at runtime.

See [Program.cs](src/browser-automation-csharp-maf-sample-foundry/Program.cs) for the full implementation.

## Repository layout

| Path | Purpose |
| --- | --- |
| `Program.cs` | Agent wiring — config, middleware pipeline, hosting setup. |
| `utils/Middlewares.cs` | Function invocation middleware (logging + `create_session` interception) and streaming middleware (live view URL injection). |
| `utils/Tools.cs` | Tool factory methods (`run_playwright_cli`, `close_browser_session`, `get_live_view_url`) and URL storage accessors. |
| `utils/BrowserSession.cs` | Playwright CLI subprocess runner with redaction and logging. |
| `utils/ToolboxScopedCredential.cs` | Token credential wrapper that overrides the toolbox auth scope. |
| `prompts/base.md` | Browser lifecycle, safety, cleanup, web extraction, and form-filling rules. |
| `skills/azure-playwright-browser-automation/SKILL.md` | Playwright CLI operational reference for remote Azure Playwright Service sessions. |

## Prerequisites

- An Azure AI Foundry project with a deployed chat model (e.g., `gpt-4.1`).
- An Azure Playwright workspace and access token. If you do not have a workspace, follow [Create a workspace](https://learn.microsoft.com/azure/app-testing/playwright-workspaces/quickstart-run-end-to-end-tests?tabs=playwrightcli&pivots=playwright-test-runner#create-a-workspace).
- Azure CLI installed and authenticated (`az login`).
- Docker, if you want to build the container locally.
- .NET 10 SDK for local development.

For hosted-agent setup, see [Deploy hosted agents with azd](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd).

## Configuration

This sample uses two kinds of configuration:

- **Runtime environment variables** are read by the C# agent process. Use these for local runs, or set them in the hosted agent environment when deploying.
- **`azd` provisioning parameters** are read by `azd provision` from the azd environment. Use these only when you want this sample to create the Playwright connection and toolbox for you.

### Runtime environment variables

For local development, copy `.env.example` to `.env` or set these values in your shell. The app loads `.env` when it starts.

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
```

Or in PowerShell:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME="gpt-4.1"
```

| Variable | Default | Purpose |
| --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | Required locally; provided by hosted agent runtime when deployed | Foundry project endpoint used for model and Toolbox MCP calls. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Required | Model deployment name. For hosted deployment, this is set from the model deployment selected during `azd ai agent init`; for local runs, set it in your shell or `.env` file. |
| `TOOLBOX_NAME` | `browser-automation-tools` | Foundry Toolbox name declared in `azure.yaml`. The default `browser-automation-tools` is hardcoded in the manifest; override only if using a different pre-existing toolbox. |
| `BROWSER_AGENT_PLAYWRIGHT_CLI_TIMEOUT_SECONDS` | `180` | Optional timeout for each Playwright CLI command. |
| `BROWSER_AGENT_MCP_TIMEOUT_SECONDS` | `120` | Optional timeout for Toolbox MCP calls. |

The Toolbox endpoint is resolved as `<FOUNDRY_PROJECT_ENDPOINT>/toolboxes/<TOOLBOX_NAME>/mcp?api-version=v1` and authenticated with the hosted agent identity.

### Provisioning parameters

`PLAYWRIGHT_SERVICE_URL`, `PLAYWRIGHT_SERVICE_RESOURCE_ID`, and `PLAYWRIGHT_SERVICE_ACCESS_TOKEN` are not read by the C# agent at runtime. They are `azd` provisioning inputs used by [`azure.yaml`](azure.yaml) to create a `PlaywrightWorkspace` project connection with API key authentication and the default `browser-automation-tools` toolbox wired to that connection. `PLAYWRIGHT_SERVICE_ACCESS_TOKEN` is marked as a secret parameter in the manifest.

Set these values with `azd env set` before running `azd provision`. `azd` stores them in `.azure/<environment-name>/.env`; the sample's root `.env` file is only for local execution.

## Running the Agent Host

### Local setup

Install dependencies and run the hosted-agent server locally:

```bash
dotnet restore browser-automation.csproj
npm install -g @playwright/cli@latest
playwright-cli install --skills
dotnet run --project browser-automation.csproj
```

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

Choose one toolbox setup path:

### Option 1: Let this sample provision the toolbox

Use this path if you want `azd provision` to create the Foundry project connection to your Azure Playwright workspace and the default `browser-automation-tools` toolbox.

Set the Playwright workspace values in your `azd` environment:

```bash
azd env set PLAYWRIGHT_SERVICE_URL "wss://<region>.api.playwright.microsoft.com/playwrightworkspaces/<workspace-id>/browsers"
azd env set PLAYWRIGHT_SERVICE_RESOURCE_ID "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.LoadTestService/playwrightWorkspaces/<workspace-name>"
azd env set PLAYWRIGHT_SERVICE_ACCESS_TOKEN "<playwright-workspace-access-token>"
```

If these are not set, running `azd ai agent init -m <azure.yaml>` will prompt you to enter them interactively.

Run `azd provision` before `azd deploy`:

```bash
azd provision
```

### Option 2: Use an existing toolbox

Use this path if your Foundry project already has a compatible toolbox. You can skip `azd provision` and set only the runtime toolbox name used by the deployed hosted agent. The toolbox must include the `browser_automation_preview` tool and its Playwright workspace connection.

```bash
azd env set TOOLBOX_NAME "<your-toolbox-name>"
```

You do not need to set `TOOLBOX_NAME` when using the default sample-provisioned toolbox name, `browser-automation-tools`.

Then deploy the hosted agent:

```bash
azd deploy
```

## Customize the sample

- Change prompt behavior in `prompts/base.md`.
- Add deeper procedural knowledge as skills under `skills/`.
- Add new tools in `utils/Tools.cs`.
- Modify middleware logic in `utils/Middlewares.cs`.

## Guidance

This sample is intended as a starting point, not a production-ready browser automation platform. Before using it in production, review authentication, network access, data handling, secret management, logging, browser permissions, and approval flows for state-changing actions.

The `run_playwright_cli` tool intentionally invokes only `playwright-cli` with a named session and optional `PLAYWRIGHT_MCP_CDP_ENDPOINT`; it does not expose general shell execution.

The default hosted container resources (`cpu: "0.25"`, `memory: "0.5Gi"`) are minimal. Increase them in `azure.yaml` for multi-step scraping, longer QA sessions, or data-heavy browser automation.

Useful references:

- [Hosted agents in Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [Agent Framework overview](https://learn.microsoft.com/en-gb/agent-framework/overview/?pivots=programming-language-csharp)
- [Agent Framework skills](https://learn.microsoft.com/en-gb/agent-framework/agents/skills?pivots=programming-language-csharp)
- [Playwright CLI](https://github.com/microsoft/playwright-cli)
