**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight.

# GitHub Copilot SDK — Invocations Protocol (Streaming)

A minimal getting-started agent using the [GitHub Copilot SDK](https://pypi.org/project/github-copilot-sdk/) (`CopilotClient`) with the [azure-ai-agentserver-invocations](https://pypi.org/project/azure-ai-agentserver-invocations/) protocol. Streams raw Copilot SDK session events as SSE with multi-turn support.

## How It Works

1. Receives `{"input": "..."}` via `POST /invocations`
2. On first request, tries to resume a persisted Copilot session (by `FOUNDRY_AGENT_SESSION_ID`); if none exists, creates a new one
3. Each `SessionEvent` from the Copilot SDK is streamed back as an SSE `data:` event using `event.to_dict()`
4. A final `event: done` signal marks the end of the response
5. The session is cached in memory and reused across requests for multi-turn conversation
6. Skills in the `skills/` directory are auto-loaded — e.g. the included `joke` skill makes Copilot respond in pirate tone

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `GITHUB_TOKEN` | Yes | GitHub fine-grained PAT with **Copilot Requests → Read-only** permission |
| `FOUNDRY_AGENT_SESSION_ID` | No | Session ID for persistence/resume. If unset, a UUID is generated |

## Running Locally

### Prerequisites

- Python 3.10+
- A GitHub fine-grained PAT (`github_pat_` prefix)

Create one at [github.com/settings/personal-access-tokens/new](https://github.com/settings/personal-access-tokens/new) with **Account permissions → Copilot Requests → Read-only**.

> **Note:** Classic tokens (`ghp_`) are not supported. Use a fine-grained PAT (`github_pat_`), OAuth token (`gho_`), or GitHub App user token (`ghu_`).

### Using `azd` (Recommended)

Add the `GITHUB_TOKEN` to the environment variables first:

```bash
azd env set GITHUB_TOKEN="github_pat_..."
```

Next, start the agent locally with the `run` command:

```bash
azd ai agent run
```

The agent starts on `http://localhost:8088/`.

### Without `azd`

```bash
pip install -r requirements.txt
cp .env.example .env  # then set GITHUB_TOKEN
python main.py
```

The agent starts on `http://localhost:8088/`.

## Invoke with azd

### Local

**Bash:**
```bash
azd ai agent invoke --local '{"input": "What can you help me with?"}'
```

**PowerShell:**
```powershell
azd ai agent invoke --local '{\"input\": \"What can you help me with?\"}'
```

### Test with curl

```bash
# First message
curl -N -X POST http://localhost:8088/invocations \
  -H "Content-Type: application/json" \
  -d '{"input": "What is Python?"}'

# Follow-up (multi-turn — same session remembers context)
curl -N -X POST http://localhost:8088/invocations \
  -H "Content-Type: application/json" \
  -d '{"input": "Give me a code example"}'
```

### SSE Event Format

Each Copilot SDK event is streamed via `event.to_dict()`:

```
data: {"type": "assistant.message_delta", "data": {"delta_content": "Python is"}}\n\n
data: {"type": "assistant.message_delta", "data": {"delta_content": " a programming"}}\n\n
...
event: done
data: {"invocation_id": "...", "session_id": "..."}
```

## Deploying the Agent to Microsoft Foundry

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
azd ai agent invoke '{"input": "What can you help me with?"}'
```

**PowerShell:**
```powershell
azd ai agent invoke '{\"input\": \"What can you help me with?\"}'
```

To stream logs from the running agent:

```bash
azd ai agent monitor
```

For the full deployment guide, see [Azure AI Foundry hosted agents](https://aka.ms/azdaiagent/docs).

## Adding Skills

Any subdirectory under `skills/` containing a `SKILL.md` file is automatically loaded by the Copilot SDK. The included `joke` skill demonstrates this:

```
skills/
└── joke/
    └── SKILL.md    ← tells Copilot to respond like a pirate
```

To add your own skill, create a new folder under `skills/` with a `SKILL.md`:

```bash
mkdir skills/my-skill
cat > skills/my-skill/SKILL.md << 'EOF'
---
name: my-skill
description: What this skill does.
---

# My Skill

Instructions for Copilot when this skill is active.
...
```

## Troubleshooting

### Images built on Apple Silicon or other ARM64 machines do not work on our service

We **recommend deploying with `azd deploy`**, which uses ACR remote build and always produces images with the correct architecture.

If you choose to **build locally**, and your machine is **not `linux/amd64`** (for example, an Apple Silicon Mac), the image will **not be compatible with our service**, causing runtime failures.

**Fix for local builds:**

```bash
docker build --platform=linux/amd64 -t image .
```

This forces the image to be built for the required `amd64` architecture.