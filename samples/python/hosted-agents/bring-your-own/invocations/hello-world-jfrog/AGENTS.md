# Coding Agent Instructions

This project is a **Microsoft Foundry hosted agent** — a containerized AI agent that runs in [Foundry Agent Service](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents). The platform handles containerization, hosting, security, scaling, and observability so you can focus on agent logic.

This variant deploys a **pre-built image from a private JFrog Artifactory registry**, pulled using Microsoft Entra workload identity federation. See [README.md](./README.md) for the full setup.

## Key files

- `Dockerfile` — container definition
- `azure.yaml` — `project` and `language` are intentionally empty so azd uses the pre-built `image`
- `deploy/create-registry-connection.ps1` — one-time Foundry registry connection setup
- `deploy/create-agent-version.ps1` — creates the agent version with `registry_connection_id`

## Development workflow

The **Azure Developer CLI (`azd`)** manages most of the lifecycle:

```bash
azd ai agent run                           # Run locally on http://localhost:8088
azd ai agent invoke --local "your message" # Test the local agent
azd deploy                                 # Register the agent in Foundry
azd ai agent invoke "your message"         # Invoke the deployed agent
```

> [!NOTE]
> `azd` cannot yet attach the registry connection that authorizes the JFrog pull, so
> `deploy/create-agent-version.ps1` must be run after `azd deploy`. The team is working
> to make this completely deployable via `azd`; see "The temporary gap" in the README.

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