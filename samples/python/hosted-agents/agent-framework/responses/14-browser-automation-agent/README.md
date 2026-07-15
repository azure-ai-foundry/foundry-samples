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

See [main.py](src/browser-automation-python-maf-sample-foundry/main.py) for the full implementation and [docs/sample-structure.md](docs/sample-structure.md) for the design rationale.

## Repository layout

| Path | Purpose |
| --- | --- |
| `main.py` | Entry point: loads settings, builds the agent, and starts `ResponsesHostServer`. |
| `utils/` | Agent construction (`agent_factory.py`), tools, settings, logging, and path helpers. |
| `prompts/base.md` | Browser lifecycle, safety, cleanup, web extraction, and form-filling rules. |
| `skills/azure-playwright-browser-automation/SKILL.md` | Playwright CLI operational reference for remote Azure Playwright Service sessions. |
| `requirements.txt` | Python dependencies (agent-framework, azure-identity, etc.). |
| `docs/sample-structure.md` | Design notes explaining the sample structure and extension points. |

## Prerequisites

- An Azure AI Foundry project with a deployed chat model (e.g., `gpt-4.1`).
- An Azure Playwright workspace. If you do not have a workspace, follow [Create a workspace](https://learn.microsoft.com/azure/app-testing/playwright-workspaces/quickstart-run-end-to-end-tests?tabs=playwrightcli&pivots=playwright-test-runner#create-a-workspace).
- The Foundry project's managed identity must have the **Playwright Workspace Contributor** role on the Azure Playwright workspace (see [RBAC setup](#rbac-setup) below).
- Azure CLI installed and authenticated (`az login`).
- Docker, if you want to build the container locally.
- Python 3.11 or later and `uv` (or `pip`) for local development.

For hosted-agent setup, see [Deploy hosted agents with azd](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?pivots=azd).

## Configuration

This sample uses two kinds of configuration:

- **Runtime environment variables** are read by the Python agent process. Use these for local runs, or set them in the hosted agent environment when deploying.
- **`azd` provisioning parameters** are read by `azd provision` from the azd environment. Use these only when you want this sample to create the Playwright connection and toolbox for you.

### Runtime environment variables

For local development, copy `.env.example` to `.env` or set these values in your shell. The Python app loads `.env` when it starts.

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

`PLAYWRIGHT_SERVICE_URL` and `PLAYWRIGHT_SERVICE_RESOURCE_ID` are not read by the Python agent at runtime. They are `azd` provisioning inputs used by [`azure.yaml`](azure.yaml) to create a `PlaywrightWorkspace` project connection with **ProjectManagedIdentity** authentication and the default `browser-automation-tools` toolbox wired to that connection.

Set these values with `azd env set` before running `azd provision`. `azd` stores them in `.azure/<environment-name>/.env`; the sample's root `.env` file is only for local Python execution.

## Running the Agent Host

### Local setup

Install dependencies and run the hosted-agent server locally:

```bash
pip install -r requirements.txt
python main.py
```

Or using `uv`:

```bash
uv pip install -r requirements.txt
uv run main.py
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

### Test in VS Code (Foundry Toolkit)

**Prerequisites**

1. **VS Code** with the **[Foundry Toolkit](https://marketplace.visualstudio.com/items?itemName=ms-windows-ai-studio.windows-ai-studio)** extension installed.
2. For debugging Python in VS Code, install the **[Python](https://marketplace.visualstudio.com/items?itemName=ms-python.python)** extension pack.

**Set up the Python virtual environment**

- Open the Command Palette (`Ctrl+Shift+P`) and run **Python: Create Environment...** to create a virtual environment in the workspace (or **Python: Select Interpreter** to use an existing one).
- Install dependencies in the virtual environment:

  ```bash
  # use uv to accelerate
  pip install uv
  uv pip install -r requirements.txt

  # or pure pip
  pip install -r requirements.txt
  ```

**Run and debug the agent**

Press **F5** to start the agent. The agent starts and the **Agent Inspector** opens automatically. Chat with the agent in the Inspector.

**Or run manually, then open the Inspector**

1. Set the required environment variables and sign in to Azure with the Azure CLI (`az login`).
2. Start the agent: `python main.py` (listens on `http://localhost:8088`).
3. Command Palette (`Ctrl+Shift+P`) → **Foundry Toolkit: Open Agent Inspector**.

Type the following in the Inspector:

```
Open https://example.com and report the page title.
```

## Deploying the Agent to Foundry

To host the agent on Foundry, follow the instructions in the [Deploying the Agent to Foundry](../../README.md#deploying-the-agent-to-foundry) section of the README in the parent directory.

When running `azd ai agent init -m ./14-browser-automation-agent/azure.yaml` from the parent directory (one level above this sample folder), you can customize the hosted agent name with the `AGENT_NAME` parameter. Leave it blank to use the default name, `browser-automation-python-maf-sample-foundry`.

> [!IMPORTANT]
> Run `azd ai agent init` from a directory **outside** this sample folder — either a new empty directory, or one level up from this sample (i.e. `samples/python/hosted-agents/agent-framework/responses/`). Do **not** run it from inside `14-browser-automation-agent/` itself. Because the sample folder already contains `azure.yaml`, initializing in place fails with:
>
> ```
> ERROR: a project azure.yaml already exists in '.', so the sample's unified
> azure.yaml cannot be adopted there
> ```
>
> Using the parent-directory invocation shown above (or a fresh empty folder with the remote manifest URL) avoids this.

The same init flow also asks for the model deployment because [`azure.yaml`](azure.yaml) declares a `model` resource named `AZURE_AI_MODEL_DEPLOYMENT_NAME`. The selected deployment is used for the generated Azure deployment configuration and for the hosted agent's `AZURE_AI_MODEL_DEPLOYMENT_NAME` runtime environment variable. It does not update the sample's local `.env` file; set that file separately only when running the agent locally.

Choose one toolbox setup path:

### Option 1: Let this sample provision the toolbox

Use this path if you want `azd provision` to create the Foundry project connection to your Azure Playwright workspace and the default `browser-automation-tools` toolbox.

Set the Playwright workspace values in your `azd` environment:

```bash
azd env set PLAYWRIGHT_SERVICE_URL "wss://<region>.api.playwright.microsoft.com/playwrightworkspaces/<workspace-id>/browsers"
azd env set PLAYWRIGHT_SERVICE_RESOURCE_ID "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.LoadTestService/playwrightWorkspaces/<workspace-name>"
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

Or in PowerShell:

```powershell
azd env set TOOLBOX_NAME "<your-toolbox-name>"
```

You do not need to set `TOOLBOX_NAME` when using the default sample-provisioned toolbox name, `browser-automation-tools`.

The deployed hosted agent identity needs Foundry access at runtime to call the model and authenticate against the Toolbox MCP endpoint. The deployment tooling handles standard hosted-agent RBAC assignments when your account has sufficient permissions.

For option 1, the Playwright workspace connection uses **ProjectManagedIdentity** authentication — the project's managed identity must have the **Playwright Workspace Contributor** role on the workspace (see [RBAC setup](#rbac-setup)). For option 2, make sure the existing toolbox's Playwright workspace connection already has valid authentication configured.

Then deploy the hosted agent:

```bash
azd deploy
```

## RBAC setup

The Foundry project's managed identity must have the following role on the Azure Playwright workspace:

| Role | Purpose |
| --- | --- |
| **Playwright Workspace Contributor** | Grants access to create and manage browser sessions |

Assign the role using the Azure CLI:

```bash
# Get your project's managed identity principal ID from the Foundry portal or Azure CLI
PRINCIPAL_ID="<project-managed-identity-object-id>"
PWW_RESOURCE_ID="/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.LoadTestService/playwrightWorkspaces/<workspace-name>"

az role assignment create --assignee "$PRINCIPAL_ID" --role "Playwright Workspace Contributor" --scope "$PWW_RESOURCE_ID"
```

## Customize the sample

- Change prompt behavior in `prompts/base.md`.
- Add deeper procedural knowledge as skills under `skills/`.
- Add new tools in `src/browser-automation-python-maf-sample-foundry/tools.py`.

See [docs/sample-structure.md](docs/sample-structure.md) for the design rationale.

## Guidance

This sample is intended as a starting point, not a production-ready browser automation platform. Before using it in production, review authentication, network access, data handling, secret management, logging, browser permissions, and approval flows for state-changing actions.

The `run_playwright_cli` tool intentionally invokes only `playwright-cli` with a named session and optional `PLAYWRIGHT_MCP_CDP_ENDPOINT`; it does not expose general shell execution.

The default hosted container resources (`cpu: "0.25"`, `memory: "0.5Gi"`) are minimal. Increase them in `azure.yaml` for multi-step scraping, longer QA sessions, or data-heavy browser automation.

Useful references:

- [Hosted agents in Microsoft Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [Agent Framework overview](https://learn.microsoft.com/en-gb/agent-framework/overview/?pivots=programming-language-python)
- [Agent Framework skills](https://learn.microsoft.com/en-gb/agent-framework/agents/skills?pivots=programming-language-python)
- [Playwright CLI](https://github.com/microsoft/playwright-cli)
