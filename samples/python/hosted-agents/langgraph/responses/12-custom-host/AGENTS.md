# Coding Agent Instructions

This project is a Microsoft Foundry hosted agent built with LangGraph and the Responses protocol.

## Key files

- `azure.yaml` - Foundry hosted-agent manifest
- `src/langgraph-custom-host-responses/main.py` - custom request adapter and graph
- `src/langgraph-custom-host-responses/Dockerfile` - container definition

## Development workflow

```bash
azd ai agent run --no-client
# Use the README curl example to send x-client-user-locale and a location message.
azd deploy
```

## Microsoft Foundry Skill

This project was built with the microsoft-foundry skill. Before working on or answering questions about Foundry agents, read that skill first.

Install the skill with:

```bash
npx skills add https://github.com/microsoft/azure-skills --skill microsoft-foundry
```

## References

- [Hosted agents overview](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents)
- [LangGraph](https://langchain-ai.github.io/langgraph/)
- [Microsoft Foundry Skill](https://learn.microsoft.com/en-us/azure/foundry/how-to/develop/use-microsoft-foundry-skill)
