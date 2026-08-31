# Prompt Agent with MCP Tools

A prompt agent that calls tools on a remote [MCP](https://modelcontextprotocol.io/) server. The agent and its `azure.ai.connection` sibling in [`azure.yaml`](./azure.yaml) point at the public **Microsoft Learn** MCP server. `require_approval: always` asks for approval before each call; set it to `never` for unattended use.

## Deploy

```bash
azd up
azd ai agent invoke "How do I create a hosted agent with azd? Cite the Microsoft Learn docs."
```

## GitHub Copilot Harness

Supported. To use the GitHub Copilot harness, add this block to the agent service in [`azure.yaml`](./azure.yaml):

```yaml
harness:
  type: github_copilot_preview
```
