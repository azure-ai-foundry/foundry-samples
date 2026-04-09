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
| `AZURE_AI_PROJECT_ENDPOINT` | Yes | Foundry project endpoint URL |
| `GITHUB_TOKEN` | Yes | GitHub fine-grained PAT with Copilot Requests read permission |
| `FOUNDRY_TOOLBOX_ENDPOINT` | No | Toolbox MCP endpoint URL **with `?api-version=v1`** (falls back to skills-only if not set) |
| `GITHUB_COPILOT_MODEL` | No | Override the Copilot model |

## Supported Toolbox Tools

Shared toolbox tool/auth definitions live in [../SUPPORTED_TOOLBOX_TOOLS.md](../SUPPORTED_TOOLBOX_TOOLS.md).

For runnable SDK examples of creating toolbox resources, see [../sample_toolboxes_crud.py](../sample_toolboxes_crud.py).

> **Note:** Tool names from the toolbox MCP endpoint are prefixed with `server_label.` (e.g., `gitmcp.fetch_agent_docs`). The Copilot SDK rejects names containing dots, so `agent.py` automatically sanitizes them (dots/hyphens → underscores) while preserving the original MCP name for `tools/call` forwarding.

---

## Setting Up

### 1. Create a GitHub Token

Go to [github.com/settings/personal-access-tokens/new](https://github.com/settings/personal-access-tokens/new) and create a fine-grained token with:

- **Account permissions -> Copilot Requests -> Read-only**

Copy the token (starts with `github_pat_`).

> **Note:** Classic tokens (`ghp_` prefix) are not supported by the Copilot SDK. You must use a fine-grained PAT (`github_pat_`), OAuth token (`gho_`), or GitHub App user token (`ghu_`).

### 2. Create a Toolbox (Optional)

Create a toolbox resource in your Foundry project. See the [LangGraph toolbox sample](../langgraph/) for full documentation on all tool types and [../sample_toolboxes_crud.py](../sample_toolboxes_crud.py) for SDK examples.

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

```

### 3. Configure Environment

Copy `dot-env.template` to `.env` and fill in the values:

```bash
cp dot-env.template .env
```

```env
AZURE_AI_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>
GITHUB_TOKEN=github_pat_...
FOUNDRY_TOOLBOX_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1
```

### 4. Important: Toolbox Endpoint URL

The toolbox MCP endpoint URL **must** include `?api-version=v1`:

```
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/mcp?api-version=v1
```

Without the `?api-version=v1` query parameter, the endpoint returns HTTP 400.

## Running Locally

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
├── dot-env.template        # Environment variables template
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
