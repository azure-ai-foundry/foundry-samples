# What this sample demonstrates

A hosted agent that answers questions over **two distinct file knowledge sources** through scoped, security-hardened tools — the **Agent with File Tools (Responses Protocol)** sample built on the [Agent Framework](https://github.com/microsoft/agent-framework).

- **Bundled files** (image-baked, `/app/resources/`) — files the author packages with the agent at build time. Always available, identical to every session.
- **Session files** (per-session `$HOME` volume, `/home/session/`) — files the user uploads at runtime via `azd ai agent files upload`. Live for the lifetime of the session.

## How It Works

The agent registers four C# functions as tools, one tool pair per source:

| Tool | Source | Root inside container |
|------|--------|------|
| `ListBundledFiles` | Bundled (image-baked) | `/app/resources/` |
| `ReadBundledFile` | Bundled (image-baked) | `/app/resources/` |
| `ListSessionFiles` | Session-uploaded | `$HOME` (`/home/session/`) |
| `ReadSessionFile` | Session-uploaded | `$HOME` (`/home/session/`) |

Each `Read*` tool takes a `fileName` (no path components allowed) and enforces three layers of defence inside the implementation:

1. **`Path.GetFileName(input)`** strips any directory parts from the model-supplied name. `"../../etc/passwd"` becomes `"passwd"`.
2. **`Path.GetFullPath(Combine(root, name))`** canonicalises the path.
3. **`fullPath.StartsWith(root + DirectorySeparatorChar)`** rejects anything that resolves outside the tool's root.

Failures return a controlled `"File '<input>' not found in <scope>."` rather than throwing or exposing the canonical path. The model cannot read or list arbitrary container paths, even via indirect prompt injection in an uploaded file.

See [Program.cs](Program.cs) for the full implementation.

## Running the Agent Locally

### Prerequisites

Before running this sample, ensure you have:

1. **Azure Developer CLI (`azd`)** (recommended)
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) and the AI agent extension: `azd ext install azure.ai.agents`
   - Authenticated: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **.NET 10.0 SDK or later**
   - Verify your version: `dotnet --version`
   - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

> [!NOTE]
> You do **not** need an existing [Microsoft Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/what-is-foundry?view=foundry) project or model deployment to get started — `azd provision` creates them for you. If you already have a project, see the [note below](#using-azd-recommended-for-cli-workflows) on how to target it.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers; set automatically by `azd ai agent run` locally. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name — must match your Foundry project deployment. Declared in `agent.manifest.yaml`. |
| `BUNDLED_FILES_DIR` | No | Override the bundled-files root the tools read from. Defaults to `<process base dir>/resources` (`/app/resources/` in container). |
| `HOME` | No | The per-session sandbox volume root the session-files tools read from. Set by the Foundry platform; can be overridden for local testing. Defaults to `/home/session`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

**Local development (without `azd`):**

```bash
# Set env vars directly — .NET does not natively read .env files
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.services.ai.azure.com/api/projects/<project>"
export AZURE_AI_MODEL_DEPLOYMENT_NAME="<your-model-deployment-name>"
```

> [!NOTE]
> When using `azd ai agent run`, environment variables are handled automatically — no manual setup needed.

### Installing Dependencies

> [!NOTE]
> If using `azd ai agent run`, dependencies are restored automatically — skip to [Running the Sample](#running-the-sample).

Dependencies are restored automatically when building the project:

```bash
dotnet restore
```

### Running the Sample

The recommended way to run and test hosted agents locally is with the Azure Developer CLI (`azd`) or the Foundry Toolkit VS Code extension.

#### Using the Foundry Toolkit VS Code Extension

The [Foundry Toolkit VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository, it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Chat with a running agent using the **Agent Inspector**:

1. Start the agent locally first using **Using `azd`** or **Without `azd`** above. The agent listens on `http://localhost:8088/`.
2. Open the Command Palette (`Ctrl+Shift+P`) and run **Foundry Toolkit: Open Agent Inspector**.
3. The Inspector auto-connects to the running agent. Send messages to chat with the agent and watch the streamed responses.

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd) (recommended for CLI workflows)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample and generates Bicep infrastructure, `agent.yaml`, and env config automatically:

```bash
# Create a new folder for the agent and navigate into it
mkdir file-tools-agent && cd file-tools-agent

# Initialize from the manifest — azd reads it, downloads the sample,
# and generates Bicep infrastructure, agent.yaml, and env config
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/file-tools/agent.manifest.yaml

# Provision Azure resources (Foundry project, model deployment, App Insights)
azd provision

# Run the agent locally (handles env vars, build, and startup)
azd ai agent run
```

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/csharp/hosted-agents/agent-framework/file-tools/agent.manifest.yaml`

> [!NOTE]
> If you already have a Foundry project and model deployment, add `-p <project-id> -d <deployment-name>` to `azd ai agent init` to target existing resources. You can also skip provisioning entirely and configure env vars manually — see [Without `azd`](#without-azd).

The agent starts on `http://localhost:8088/`.

##### Try the bundled-files path

```bash
azd ai agent invoke --local "What is the headline total revenue in the contoso file?"
```

The agent calls `ListBundledFiles`, finds `contoso_q1_2026_report.txt`, calls `ReadBundledFile("contoso_q1_2026_report.txt")` (rooted at `/app/resources/`), and quotes the figure verbatim (`$1,482.6M`).

##### Try the session-files path

Upload the included demo file to the same session, then ask about it. `azd ai agent files upload` auto-resolves the session-id from the last invocation:

```bash
azd ai agent files upload ./example-upload/user_notes.txt
azd ai agent invoke --local "What magic token is in user_notes.txt?"
```

The agent calls `ListSessionFiles`, finds `user_notes.txt`, calls `ReadSessionFile("user_notes.txt")` (rooted at `$HOME`), and quotes the token.

##### Try a traversal attempt (it should be refused)

```bash
azd ai agent invoke --local "Read the file at the path '../../../etc/passwd' from the bundled files."
```

The agent's tool schema only accepts a `fileName` (no `path`), and the `Path.GetFileName` + `StartsWith(root)` defence in depth rejects anything that resolves outside the tool's root. The agent will refuse and explain that only the bundled files are available.

#### Without `azd`

If running without `azd`, set environment variables manually (see [Environment Variables](#environment-variables)), then:

```bash
dotnet run
```

You can then upload session files via raw HTTP if needed (see [`POST /agents/{name}/endpoint/sessions/{id}/files/content`](https://learn.microsoft.com/en-us/azure/foundry/agents/) in the Foundry SDK docs).

### Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

```bash
azd ai agent invoke "What is the headline total revenue in the contoso file?"
azd ai agent files upload ./example-upload/user_notes.txt
azd ai agent invoke "What magic token is in user_notes.txt?"
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

#### Deploying with the Foundry Toolkit VS Code Extension

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

## Adding more bundled files

Drop additional text files into [`resources/`](./resources/). The csproj `<Content Include="resources\**\*" CopyToOutputDirectory="PreserveNewest" />` rule picks them up on the next `dotnet build` / `docker build`.

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
