# Managed Agent with a Foundry Toolbox

A GitHub Copilot harness agent backed by a **Foundry Toolbox**, configured in [`azure.yaml`](./azure.yaml).

## Prerequisites

An existing Foundry Toolbox endpoint:

```bash
azd env set TOOLBOX_ENDPOINT <your-toolbox-mcp-endpoint>
```

## Deploy

```bash
azd up
azd ai agent invoke "Use your tools to help me with today's task."
```
