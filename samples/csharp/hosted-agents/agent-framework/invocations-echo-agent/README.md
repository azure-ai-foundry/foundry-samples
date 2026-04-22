# What this sample demonstrates

A minimal echo agent hosted as a Foundry Hosted Agent using the **Invocations protocol** and the [Agent Framework](https://github.com/microsoft/agent-framework). The agent reads the request body as plain text, passes it through a custom `EchoAIAgent`, and writes the echoed text back in the response. No LLM or Azure credentials are required — this is the **Echo Agent (Invocations Protocol)** sample.

## How It Works

The agent registers a custom `EchoAIAgent` that implements the Invocations protocol. When a POST request arrives at `/invocations` with a JSON body containing a `"message"` field, the agent echoes the input back as `"Echo: <input>"`. Because no model is involved, this sample requires no Azure OpenAI deployment or Foundry project endpoint — making it ideal for testing the hosting infrastructure in isolation.

See [Program.cs](Program.cs) and [EchoAIAgent.cs](EchoAIAgent.cs) for the full implementation.

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
> This sample does **not** call an LLM, so you do **not** need a Foundry project or model deployment. However, `azd provision` is still available if you want to set up infrastructure for deployment.

### Environment Variables

This agent does **not** require a model deployment — no `FOUNDRY_PROJECT_ENDPOINT` or `AZURE_AI_MODEL_DEPLOYMENT_NAME` is needed.

| Variable | Required | Description |
|----------|----------|-------------|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Optional | Enables telemetry. Auto-injected in hosted containers; set manually for local dev. |

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

The recommended way to run and test hosted agents locally is with the Azure Developer CLI (`azd`) or the Foundry VS Code extension.

#### Using the Foundry VS Code Extension

The [Foundry VS Code extension](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) has a built-in sample gallery. You can open this sample directly from the extension without cloning the repository — it scaffolds the project into a new workspace, generates `agent.yaml`, `.env`, and `.vscode/tasks.json` + `launch.json` automatically, and configures a one-click **F5** debug experience.

Follow the [VS Code quickstart](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=vscode) for a full step-by-step walkthrough.

#### Using [`azd`](https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-hosted-agent?view=foundry&pivots=azd) (recommended for CLI workflows)

No cloning required. Create a new folder, point `azd` at the manifest on GitHub, and it sets up the sample and generates Bicep infrastructure, `agent.yaml`, and env config automatically:

```bash
# Create a new folder for the agent and navigate into it
mkdir echo-agent && cd echo-agent

# Initialize from the manifest — azd reads it, downloads the sample,
# and generates Bicep infrastructure, agent.yaml, and env config
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/invocations-echo-agent/agent.manifest.yaml

# Provision Azure resources (Foundry project, App Insights)
azd provision

# Run the agent locally (handles env vars, build, and startup)
azd ai agent run
```

> [!NOTE]
> If you've already cloned this repository, pass a local path to the manifest instead:
> `azd ai agent init -m <path-to-repo>/samples/csharp/hosted-agents/agent-framework/invocations-echo-agent/agent.manifest.yaml`

> [!NOTE]
> If you already have a Foundry project, add `-p <project-id>` to `azd ai agent init` to target existing resources. You can also skip provisioning entirely and configure env vars manually — see [Without `azd`](#without-azd).

The agent starts on `http://localhost:8088/`. To invoke it:

**Bash:**
```bash
azd ai agent invoke --local '{"message": "Hello, world!"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"message\": \"Hello, world!\"}'
```

Or use curl directly:

```bash
curl -X POST http://localhost:8088/invocations -i \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello, world!"}'
```

The server will respond with a JSON object containing the response text. The `-i` flag includes the HTTP response headers in the output, which includes the session ID that can be used for multi-turn conversations. Here is an example of the response:

```
HTTP/1.1 200
content-type: application/json
x-agent-invocation-id: ec04d020-a0e7-441e-ae83-db75635a9f83
x-agent-session-id: 9370b9d4-cd13-4436-a57f-03b843ac0e17
x-platform-server: azure-ai-agentserver-core/2.0.0 (dotnet/10.0)

{"response":"Echo: Hello, world!"}
```

### Multi-turn conversation

To have a multi-turn conversation with the agent, take the session ID from the response headers of the previous request and include it in URL parameters for the next request:

```bash
curl -X POST "http://localhost:8088/invocations?agent_session_id=9370b9d4-cd13-4436-a57f-03b843ac0e17" -i \
  -H "Content-Type: application/json" \
  -d '{"message": "How are you?"}'
```

#### Without `azd`

If running without `azd`, set environment variables manually if needed (see [Environment Variables](#environment-variables)), then:

```bash
dotnet run
```

### Deploying the Agent to Microsoft Foundry

Once you've tested locally, deploy to Microsoft Foundry:

```bash
# Provision Azure resources (skip if already done during local setup)
azd provision

# Build, push, and deploy the agent to Foundry
azd deploy
```

After deploying, invoke the agent running in Foundry:

**Bash:**
```bash
azd ai agent invoke '{"message": "Hello, world!"}'
```

**PowerShell:**
```powershell
azd ai agent invoke '{\"message\": \"Hello, world!\"}'
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.
