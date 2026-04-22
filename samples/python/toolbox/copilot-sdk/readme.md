<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# Copilot SDK + Toolbox Agent (Responses Protocol)

This sample deploys a GitHub Copilot SDK agent wired to toolbox in Foundry,
using the **Responses** protocol. It combines the Copilot SDK's skill system with
tools(MCP, OpenAPI, AI Search, Web Search, Code Interpreter, A2A).

## Features

- **GitHub Copilot SDK**: Uses `CopilotClient` for AI reasoning, multi-turn sessions, and skill execution
- **toolbox in Foundry MCP**: Connects to a toolbox MCP endpoint, giving the agent access to remote tools
- **Skills + Tools**: Local skill directories and remote toolbox tools are both available in the same session
- **Multi-turn conversations**: Session caching with hot/warm/cold resume for conversation continuity
- **Streaming**: Full SSE streaming support via the Foundry responses protocol

## How It Works

1. The agent starts a `CopilotClient` session configured with both skill directories and the toolbox MCP endpoint
2. The Copilot SDK connects to the toolbox MCP server and discovers available tools
3. Skills (from local `SKILL.md` directories) and toolbox tools are both available during conversation
4. The agent is served via the Foundry responses protocol

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint URL (platform-injected at runtime) |
| `GITHUB_TOKEN` | Yes | GitHub fine-grained PAT with Copilot Requests read permission |
| `FOUNDRY_AGENT_TOOLBOX_ENDPOINT` | Yes | Base URL for toolbox MCP proxy (platform-injected; append `/{name}/mcp?api-version=v1`) |
| `FOUNDRY_AGENT_TOOLBOX_FEATURES` | No | Feature-flag headers for toolbox requests (platform-injected) |
| `TOOLBOX_NAME` | **Yes** | Name of your toolbox resource — must match what you created in Foundry (defaults to `agent-tools`) |
| `GITHUB_COPILOT_MODEL` | No | Override the Copilot model |

## Supported Toolbox Tools

Shared toolbox tool/auth definitions live in [../SUPPORTED_TOOLBOX_TOOLS.md](../SUPPORTED_TOOLBOX_TOOLS.md).

For runnable SDK examples of creating toolbox resources, see [../sample_toolboxes_crud.py](../sample_toolboxes_crud.py).

> **Note:** Tool names from the toolbox MCP endpoint are prefixed with `server_label.` (e.g., `gitmcp.fetch_agent_docs`). The Copilot SDK rejects names containing dots, so `agent.py` automatically sanitizes them (dots/hyphens → underscores) while preserving the original MCP name for `tools/call` forwarding.

---

## Prerequisites

- Python 3.12+
- A [Microsoft Foundry](https://ai.azure.com) account and project
- A GitHub account with access to GitHub Copilot
- Azure CLI installed and logged in:

  ```bash
  az login
  ```

## Setting Up

### 1. Create a GitHub Token

Go to [github.com/settings/personal-access-tokens/new](https://github.com/settings/personal-access-tokens/new) and create a fine-grained token with:

- **Account permissions -> Copilot Requests -> Read-only**

Copy the token (starts with `github_pat_`).

> **Note:** Classic tokens (`ghp_` prefix) are not supported by the Copilot SDK. You must use a fine-grained PAT (`github_pat_`), OAuth token (`gho_`), or GitHub App user token (`ghu_`).

### 2. Create a Toolbox (Optional)

Create a toolbox resource in your Foundry project. See the [LangGraph toolbox sample](../langgraph/) for full documentation on all tool types and [`../sample_toolboxes_crud.py`](../sample_toolboxes_crud.py) for SDK examples.

Example — create a toolbox with a public MCP server:

```bash
cat > toolbox.json << 'EOF'
{
  "name": "my-toolbox",
  "description": "Public MCP server",
  "tools": [
    {
      "type": "mcp",
      "server_label": "mslearn",
      "server_url": "https://learn.microsoft.com/api/mcp",
      "require_approval": "never"
    }
  ]
}
EOF

foundry-agent toolbox create --payload toolbox.json
```

### 3. Configure Environment

Copy `.env.example` to `.env` and fill in the values:

```bash
cp .env.example .env
```

> **Windows (PowerShell):** `Copy-Item .env.example .env`

```env
FOUNDRY_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
GITHUB_TOKEN=github_pat_...
FOUNDRY_AGENT_TOOLBOX_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes
TOOLBOX_NAME=my-toolbox
```

### 4. Toolbox Endpoint URL

The platform injects `FOUNDRY_AGENT_TOOLBOX_ENDPOINT` (base URL) at runtime. The code
appends `/{TOOLBOX_NAME}/mcp?api-version=v1` to form the full MCP proxy URL.

For local development, set the base URL manually and specify `TOOLBOX_NAME`.

**`TOOLBOX_NAME` must be set as an environment variable** — both locally and when
deployed — to match the name of your actual toolbox resource. It defaults to
`agent-tools` if unset.

## Deploying to Foundry

Use `azd ai agent init` with this sample's manifest, then set the toolbox name:

```bash
azd ai agent init \
  -m https://github.com/microsoft/hosted-agents-vnext-private-preview/blob/main/samples/python/toolbox/copilot-sdk/agent.manifest.yaml \
  --project-id /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>
```

After initialization, set required environment variables:

```bash
# Enable vNext features (required during private preview)
azd env set enableHostedAgentVNext "true"

# Your GitHub fine-grained PAT
azd env set GITHUB_TOKEN "github_pat_..."

# The name of your toolbox resource (MUST match what you created in Foundry)
azd env set TOOLBOX_NAME "my-toolbox"
```

Then provision and deploy:

```bash
azd provision
azd deploy

# Test the deployed agent
azd ai agent invoke --new-session "What tools do you have?" --timeout 120
```

> **Note:** `FOUNDRY_AGENT_TOOLBOX_ENDPOINT` and `FOUNDRY_PROJECT_ENDPOINT` are
> injected automatically by the platform at deploy time. You do not need to set them.
> Only `GITHUB_TOKEN` and `TOOLBOX_NAME` require manual configuration.

```bash
# Install dependencies
pip install -r requirements.txt

# Start the agent
python main.py
```

## Adding Skills

Any directory at the project root containing a `SKILL.md` file is automatically discovered as a skill. The included `greeting/` directory is an example.

```
copilot-toolbox/
├── greeting/
│   └── SKILL.md       <- discovered as a skill
├── my-new-skill/
│   └── SKILL.md       <- add your own skill
└── ...
```

## Protocol

This sample uses the **Responses Protocol** via `azure-ai-agentserver-core`, which provides:

- OpenAI-compatible `/responses` endpoint
- Streaming SSE support
- Multi-turn conversation with `previous_response_id`

## Project Structure

```
copilot-toolbox/
├── main.py                 # Entrypoint — skill discovery + agent creation
├── agent.py                # CopilotToolboxAgent — Copilot SDK + toolbox MCP
├── server.py               # Foundry responses protocol adapter
├── _telemetry.py           # Azure Monitor / App Insights setup
├── greeting/SKILL.md       # Example skill
├── agent.yaml.template     # Deployment manifest
├── .env.example            # Environment variables template
├── Dockerfile              # Container build
├── requirements.txt        # Python dependencies
└── .dockerignore           # Docker build exclusions
```

## Comparison with Other Samples

| Sample | LLM Engine | Tool Source | Protocol |
|--------|-----------|-------------|----------|
| `skills/` (template) | Copilot SDK | Local skill directories only | Responses |
| `toolbox/` (sample) | Azure OpenAI via LangGraph | Toolbox MCP endpoint | Responses |
| **`copilot-toolbox/`** | **Copilot SDK** | **Toolbox MCP + local skills** | **Responses** |

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
