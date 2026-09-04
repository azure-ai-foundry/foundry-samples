# Coding Agent Instructions

This project is a **Microsoft Foundry hosted agent** — a containerized AI agent that runs in [Foundry Agent Service](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents). The platform handles containerization, hosting, security, scaling, and observability so you can focus on agent logic.

## Key files

- `src/uv-pyproject-python-responses/main.py` — agent implementation
- `src/uv-pyproject-python-responses/pyproject.toml` — project metadata and dependencies
- `src/uv-pyproject-python-responses/uv.lock` — reproducible dependency lock
- `src/uv-pyproject-python-responses/uv.toml` — uv configuration for system TLS certificates
- `src/uv-pyproject-python-responses/Dockerfile` — optional container definition

## Development workflow

The **Azure Developer CLI (`azd`)** manages the full lifecycle:

```bash
azd ai agent run                           # Run locally on http://localhost:8088
azd ai agent invoke --local "your message" # Test the local agent
azd deploy                                 # Deploy to Foundry
azd ai agent invoke "your message"         # Invoke the deployed agent
```

Use `uv sync --frozen` after changing dependencies. Run `uv lock` and commit the updated `uv.lock` when dependency declarations change.

## Microsoft Foundry Skill

Install the **Microsoft Foundry Skill** for guided deployment, evaluation, and troubleshooting workflows.

Direct install (preferred, works with any coding agent):

```bash
npx skills add https://github.com/microsoft/azure-skills --skill microsoft-foundry
```

Or install the Azure Skills Plugin:

- **Copilot CLI**: `/plugin marketplace add microsoft/azure-skills` then `/plugin install azure@azure-skills`
- **Claude Code**: `/plugin install azure@claude-plugins-official`

Then ask naturally, e.g. `Use the Microsoft Foundry Skill to deploy this agent.`

## References

- [Hosted agents overview](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [Microsoft Foundry Skill](https://learn.microsoft.com/en-us/azure/foundry/how-to/develop/use-microsoft-foundry-skill)
