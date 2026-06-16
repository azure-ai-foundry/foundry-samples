<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency note for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

> [!IMPORTANT]
> Agent Optimizer is currently in limited preview and only available through a sign-up process. To access the service, complete the [intake form](https://aka.ms/ao/preview-form). This preview is provided without a service-level agreement, and we don't recommend it for production workloads. Certain features might not be supported or might have constrained capabilities. For more information, see [Supplemental Terms of Use for Microsoft Azure Previews](https://azure.microsoft.com/en-us/support/legal/preview-supplemental-terms/).

# What this sample demonstrates

A minimal "hello world" hosted agent using the **Bring Your Own** approach with the **Responses protocol** — ready for the **agent optimizer in Foundry Agent Service**. It shows how to wire optimization config loading into the simplest possible agent so the agent optimizer can evaluate and improve it.

Use this sample to:
- Understand the optimization config loading pattern (`load_config()`)
- Verify the end-to-end optimization flow (deploy → evaluate → optimize → apply)
- Get a baseline working before adding business logic

## How It Works

### Model Integration

The agent uses the Foundry SDK to create an OpenAI-compatible Responses client. When a request arrives, the handler loads optimized config (instructions, model) via `load_config()`, calls the model, and returns the reply as a `TextResponse`.

### Optimization Config Loading

At startup (and on each request), the agent calls `load_config()` from the `azure-ai-agentserver-optimization` package. This resolves config in priority order:

1. **Optimization candidate** — `OPTIMIZATION_CANDIDATE_ID` env var (set by the agent optimizer during evaluation)
2. **Local baseline** — `.agent_configs/baseline/instructions.md` + `metadata.yaml`
3. **Hardcoded fallback** — Only if no baseline exists

This means the agent works identically with or without optimization — no feature flags needed.

## Running the Agent Locally

### Prerequisites

1. **Azure Developer CLI (`azd`)** v1.25.3+
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
   - Install the agent optimizer extension: `azd ext install azure.ai.agents`
   - Authenticate: `azd auth login`

2. **Azure CLI**
   - Installed and authenticated: `az login`

3. **Python 3.12+**

### Environment Variables

See [`.env.example`](.env.example) for the full list.

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name (e.g., `gpt-4.1-mini`). |
| `OPTIMIZATION_LOCAL_DIR` | Yes | Path to agent config directory (default: `.agent_configs`). |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. Auto-injected in hosted containers. |

## Option 1: Azure Developer CLI (`azd`)

### Initialize the agent project

```bash
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/bring-your-own/responses/optimization-hello-world/agent.manifest.yaml
```

The interactive flow prompts for your Azure subscription, region, and model deployment settings. This generates `azure.yaml`, infrastructure-as-code files, and configures the environment.

### Provision and deploy

```bash
cd optimization-hello-world-python-responses
az login
azd auth login
azd provision
azd deploy
```

### Invoke the agent

```bash
azd ai agent invoke "Hello! What can you help me with?"
```

## Option 2: Foundry Toolkit VS Code Extension

1. Clone this repo and open this sample folder in VS Code.
2. Start locally: `azd ai agent run`
3. Open Command Palette → **Foundry Toolkit: Open Agent Inspector** to chat with the agent.
4. When ready: **Foundry Toolkit: Deploy Hosted Agent**.

## Running Optimization

This sample ships with `eval.yaml` and `eval.jsonl` — everything needed to optimize out of the box.

### Run optimization

```bash
azd ai agent optimize
```

The interactive flow prompts you to select:

- **Eval model** — the model deployment used to score evaluation results (e.g., `gpt-4.1-mini`).
- **Optimization model** — the model deployment used to generate improved candidates (e.g., `gpt-5.4`).

### Monitor progress

```bash
azd ai agent optimize status <job-id> --watch
```

### Apply the best candidate

```bash
azd ai agent optimize apply --candidate <candidate-id>
```

### Verify the optimized agent

```bash
azd ai agent invoke "Hello! What can you help me with?"
```

## Files

| File | Purpose |
|------|---------|
| `main.py` | Agent entry point — Responses handler + optimization config loading |
| `agent.yaml` | Hosted agent deployment config (protocols, resources, env vars) |
| `agent.manifest.yaml` | Template manifest for `azd ai agent init` |
| `Dockerfile` | Container image build |
| `requirements.txt` | Python dependencies (includes optimization wheel) |
| `eval.yaml` | Agent optimizer configuration (dataset, evaluators, models) |
| `eval.jsonl` | Evaluation dataset with tasks and criteria |
| `.agent_configs/baseline/` | Baseline agent config (instructions, model, metadata) |
| `.env.example` | Environment variable documentation |
