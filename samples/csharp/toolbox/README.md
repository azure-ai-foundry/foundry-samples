<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# .NET Toolbox Samples

.NET samples for running Microsoft Foundry agents connected to a **Toolbox** via the
MCP Streamable HTTP protocol. Two agent framework options are provided — pick the one
that matches your stack.

## Why Toolboxes?

Building an AI agent is only half the story. The real magic happens when your agent can **do things** — search the web, read emails, query databases, call APIs. But wiring up each tool individually is tedious, fragile, and hard to manage across agents.

**A toolbox is a reusable bundle of tools, managed in Foundry, that agents consume through a single, consistent interface.**

| Without Toolbox | With Toolbox |
|---|---|
| Each agent manages its own tool connections | Tools are shared across agents from a central place |
| Auth tokens, retries, and schemas are your problem | Platform handles auth, versioning, and schema validation |
| Adding a tool means redeploying your agent | Add tools to a toolbox — agents discover them automatically |
| No standard protocol — every integration is custom | Industry-standard **MCP protocol** for all tools |

### What You Can Put in a Toolbox

| Tool Type | What It Does | Example |
|-----------|-------------|---------|
| **MCP Tool** | Connect to any MCP-compatible server | GitHub Copilot, custom APIs |
| **Web Search** | Search the internet for fresh information | Bing-powered web search |
| **File Search** | Search your uploaded documents (RAG) | Vector store search |
| **Azure AI Search** | Query Azure AI Search indexes | Enterprise knowledge bases |
| **OpenAPI Tool** | Call any REST API with an OpenAPI spec | Internal microservices |
| **Code Interpreter** | Run Python in a sandboxed environment | Data analysis, calculations |

## Which sample should I use?

| I want to… | Use |
|-------------|-----|
| Build a .NET agent with a custom ReAct loop | [`maf/`](./maf/) |
| Create, list, and delete toolbox resources from code | [`crud-sample/`](./crud-sample/) |

## Sample Comparison

| Capability | `maf/` |
|-----------|:---:|
| Multi-turn conversation | ✅ |
| Streaming (SSE) | ✅ |
| Tool schema sanitization | ✅ |
| SDK | Agent Framework |

All agent samples:
- Serve the **Responses Protocol** on port `8088`
- Authenticate to the Toolbox endpoint using `DefaultAzureCredential` (bearer token, auto-refreshed)
- Send the `Foundry-Features: Toolboxes=V1Preview` header on every MCP request (required)

## Prerequisites (all samples)

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [Microsoft Foundry](https://ai.azure.com) account and project
- A toolbox already created in that project (see [`crud-sample/`](./crud-sample/))
- Azure CLI installed and logged in:

  ```bash
  az login
  ```

## What is a Toolbox?

A **Toolbox** is a named collection of tools (MCP, OpenAPI, Azure AI Search, Web Search,
File Search, Code Interpreter, A2A) hosted in your Microsoft Foundry project. Agents
connect to a toolbox via its MCP endpoint and dynamically discover available tools at startup.

The toolbox MCP endpoint URL supports two forms:

```
# Latest version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/mcp?api-version=v1

# Pinned to a specific version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/versions/<version>/mcp?api-version=v1
```

> **Note:** The `?api-version=v1` query parameter is **required**. Requests without it return HTTP 400.

## How to Get Your Project Endpoint

1. Go to [ai.azure.com](https://ai.azure.com) and open your project.
2. Navigate to **Settings** → **Project details**.
3. Copy the **Project endpoint** value — it looks like:

   ```
   https://<account>.services.ai.azure.com/api/projects/<project>
   ```

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| HTTP 400 on MCP endpoint | Missing `?api-version=v1` in URL | Add `?api-version=v1` to toolbox endpoint |
| HTTP 401 on agent invoke | Agent's managed identity lacks RBAC | Assign "Cognitive Services OpenAI User" role to the agent's `instance_identity.principal_id` |
| "Multiple tools without identifiers" | More than one unnamed tool in a toolbox | Use `MCPTool` with `server_label` for named tools; only one unnamed tool (WebSearch, FileSearch, etc.) per toolbox |
| Agent returns empty response | RBAC propagation delay | Wait 2–5 minutes after role assignment, then retry |
| `session_not_ready` error | Container startup failure | Check `azd ai agent monitor --session-id <id>` for crash logs |
| Tool schemas rejected by OpenAI | MCP server returns malformed schemas | Sanitize schemas — add empty `properties` to `object` types missing them |

## Key Concepts Reference

### MCP Protocol

Toolboxes use **Model Context Protocol (MCP)** — an open standard for tool communication:

- **`tools/list`** — Returns all available tools with their names, descriptions, and input schemas
- **`tools/call`** — Invokes a specific tool with arguments and returns structured results

All requests use JSON-RPC 2.0 format over HTTP POST.

### Authentication

- **Agent → Toolbox:** Azure AD bearer token (scope: `https://ai.azure.com/.default`)
- **Toolbox → External Services:** Managed by the platform via project connections (API keys, OAuth, managed identity)
- **Required header:** `Foundry-Features: Toolboxes=V1Preview`

### Toolbox Endpoint Format

```
https://<ai-account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/mcp?api-version=v1
```

## Related Python Samples

The equivalent Python samples are in [`../../python/toolbox/`](../../python/toolbox/).
For toolbox creation SDK examples, see [`../../python/toolbox/sample_toolboxes_crud.py`](../../python/toolbox/sample_toolboxes_crud.py).

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
