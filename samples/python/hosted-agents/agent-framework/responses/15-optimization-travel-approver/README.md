<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency note for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

> [!IMPORTANT]
> Agent Optimizer is currently in limited preview and only available through a sign-up process. To access the service, complete the [intake form](https://aka.ms/ao/preview-form). This preview is provided without a service-level agreement, and we don't recommend it for production workloads. Certain features might not be supported or might have constrained capabilities. For more information, see [Supplemental Terms of Use for Microsoft Azure Previews](https://azure.microsoft.com/en-us/support/legal/preview-supplemental-terms/).

# What this sample demonstrates

A **travel request approval agent** built with **Agent Framework** — ready for the **agent optimizer in Foundry Agent Service**. This sample demonstrates optimization of instructions, skills, and tool descriptions in an agent that uses the Agent Framework's native tool/skill system.

Use this sample to:
- See how optimization works with Agent Framework (skills, tools, workflows)
- Understand tool description optimization (improving when/how tools are invoked)
- Learn the optimization flow with a multi-target configuration (instruction + skill + tool)

## The Scenario

**Contoso Ltd.** needs a travel approval agent that:
- Reviews travel requests against corporate policy
- Checks budget constraints and approval thresholds
- Validates destination safety and travel advisories
- Routes complex requests to human approvers
- Enforces mandatory advance booking windows

The baseline agent has deliberately weak instructions and a basic policy-reviewer skill. The agent optimizer improves all three targets:

| Optimization target | What it does | Expected improvement |
|---|---|---|
| **Instruction** | Rewrites system prompt with strict policy enforcement | Baseline ~0.50 → Optimized ~0.85+ |
| **Skill** | Improves the policy-reviewer skill procedures | Better structured policy checking |
| **Tool descriptions** | Optimizes when and how tools are invoked | More accurate tool usage |

## How It Works

### Agent Framework Integration

The agent uses `agent-framework-foundry-hosting` with `ResponsesHostServer`. Skills are loaded from the `skills/` directory and tools are registered via Agent Framework's native tool system.

### Optimization Config Loading

The agent loads optimized config (instructions, skills, model) at startup via `load_config()`:
1. **Optimization candidate** — `OPTIMIZATION_CANDIDATE_ID` env var (during evaluation)
2. **Local baseline** — `.agent_configs/baseline/`
3. **Hardcoded fallback**

### Evaluation

The `eval/` directory contains the evaluation dataset with travel request scenarios and approval criteria. Custom evaluators in `evaluators/` provide domain-specific scoring.

## Running the Agent Locally

### Prerequisites

1. **Azure Developer CLI (`azd`)** v1.25.3+
   - [Install azd](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
   - Install the agent optimizer extension: `azd ext install azure.ai.agents`
   - Authenticate: `azd auth login`

2. **Azure CLI** — `az login`

3. **Python 3.12+**

### Environment Variables

See [`.env.example`](.env.example) for the full list.

| Variable | Required | Description |
|----------|----------|-------------|
| `FOUNDRY_PROJECT_ENDPOINT` | Yes | Foundry project endpoint. Auto-injected in hosted containers. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Yes | Model deployment name (e.g., `gpt-4.1-mini`). |
| `OPTIMIZATION_LOCAL_DIR` | Yes | Path to agent config directory (default: `.agent_configs`). |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Recommended | Enables telemetry. |

## Option 1: Azure Developer CLI (`azd`)

### Initialize the agent project

```bash
azd ai agent init -m https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/python/hosted-agents/agent-framework/responses/15-optimization-travel-approver/agent.manifest.yaml
```

The interactive flow prompts for your Azure subscription, region, and model deployment settings. This generates `azure.yaml`, infrastructure-as-code files, and configures the environment.

### Provision and deploy

```bash
cd optimization-travel-approver-python-responses
az login
azd auth login
azd provision
azd deploy
```

### Invoke the agent

```bash
azd ai agent invoke "I need to book a trip to Tokyo next week for a client meeting. Budget is $5000."
```

## Option 2: Foundry Toolkit VS Code Extension

1. Clone this repo and open this sample folder in VS Code.
2. Start locally: `azd ai agent run`
3. Open Command Palette → **Foundry Toolkit: Open Agent Inspector** to chat.
4. When ready: **Foundry Toolkit: Deploy Hosted Agent**.

## Running Optimization

This sample ships with `eval.yaml` and evaluation data — run optimization out of the box.

### Run optimization

```bash
azd ai agent optimize
```

### Monitor progress

```bash
azd ai agent optimize status <job-id> --watch
```

### Apply the best candidate

```bash
azd ai agent optimize apply --candidate <candidate-id>
```

### Verify improvement

```bash
azd ai agent invoke "I need to fly to London tomorrow for an emergency meeting. No budget limit."
```

## Files

| File | Purpose |
|------|---------|
| `main.py` | Agent entry point — Agent Framework + optimization config loading |
| `agent.yaml` | Hosted agent deployment config |
| `agent.manifest.yaml` | Template manifest for `azd ai agent init` |
| `Dockerfile` | Container image build |
| `requirements.txt` | Python dependencies (Agent Framework + optimization wheel) |
| `eval.yaml` | Agent optimizer configuration (dataset, evaluators, models) |
| `eval/` | Evaluation dataset with travel request scenarios |
| `evaluators/` | Custom evaluators for domain-specific scoring |
| `.agent_configs/baseline/` | Baseline agent config (instructions, skills, model metadata) |
| `skills/` | Agent Framework skill definitions |
| `.env.example` | Environment variable documentation |
