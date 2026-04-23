<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# Python Toolbox Samples

Python samples for running Microsoft Foundry agents connected to a **toolbox in Foundry** via the
MCP Streamable HTTP protocol. Four framework options are provided — pick the one that
matches your existing stack.

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
| Get started quickly with full `azd` deployment (infra + deploy) and GitHub toolbox | [`azd/`](./azd/) |
| Write a LangGraph agent with maximum flexibility | [`langgraph/`](./langgraph/) |
| Use the Microsoft Agent Framework SDK without LangChain/LangGraph | [`maf/`](./maf/) |
| Use GitHub Copilot SDK combined with local skills and toolbox tools | [`copilot-sdk/`](./copilot-sdk/) |

## Sample Comparison

| Capability | `azd/` | `langgraph/` | `maf/` | `copilot-sdk/` |
|-----------|:---:|:---:|:---:|:---:|
| Multi-turn conversation | ✅ | ✅ | ✅ | ✅ |
| Streaming (SSE) | ✅ | ✅ | ✅ | ✅ |
| OAuth consent handling | ✅ | ✅ | ✅ | ✅ |
| Tool schema sanitization | ✅ | ✅ | ✅ | ✅ |
| Tracing | ✅ | ✅ | ✅ | ✅ |
| SDK | LangGraph | LangGraph | Microsoft Agent Framework | GitHub Copilot SDK |

All samples:
- Serve the **Responses Protocol** on port `8088`
- Authenticate to the toolbox endpoint using `DefaultAzureCredential` (bearer token, auto-refreshed)
- Read `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_AGENT_TOOLBOX_ENDPOINT`, and `AZURE_AI_MODEL_DEPLOYMENT_NAME` from the environment
- Send the `Foundry-Features: Toolboxes=V1Preview` header on every MCP request (required — requests without it are rejected)
- Support local dev via a `.env` file (copy `.env.example` → `.env` and fill in values)

## Supported Toolbox Tools

Canonical tool and auth type definitions are documented in
[SUPPORTED_TOOLBOX_TOOLS.md](./SUPPORTED_TOOLBOX_TOOLS.md).

For runnable SDK examples of creating every tool type (MCP, OpenAPI, Azure AI Search,
Bing, etc.), see [sample_toolboxes_crud.py](./sample_toolboxes_crud.py).

## Prerequisites (all samples)

- Python 3.12+
- A [Microsoft Foundry](https://ai.azure.com) project
- A toolbox already created in that project — see [`sample_toolboxes_crud.py`](./sample_toolboxes_crud.py) to create one
  (**The `azd/` sample creates the toolbox automatically during `azd deploy` — no pre-created toolbox needed**)
- Azure CLI installed and logged in:

  ```bash
  az login
  ```

## Getting Your `FOUNDRY_PROJECT_ENDPOINT`

1. Go to [ai.azure.com](https://ai.azure.com) and open your project.
2. Navigate to **Settings** → **Project details**.
3. Copy the **Project endpoint** — it looks like:

   ```
   https://<account>.services.ai.azure.com/api/projects/<project>
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

Use [`sample_toolboxes_crud.py`](./sample_toolboxes_crud.py) to create a toolbox before running any of the agent samples.

## Troubleshooting

### Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| HTTP 400 on MCP endpoint | Missing `?api-version=v1` in URL | Add `?api-version=v1` to `FOUNDRY_AGENT_TOOLBOX_ENDPOINT` |
| HTTP 401 on agent invoke | Agent's managed identity lacks RBAC | Assign "Cognitive Services OpenAI User" role to the agent's `instance_identity.principal_id` |
| "Multiple tools without identifiers" | More than one unnamed tool in a toolbox | Use `MCPTool` with `server_label` for named tools; only one unnamed tool (WebSearch, FileSearch, etc.) per toolbox |
| Agent returns empty response | RBAC propagation delay | Wait 2–5 minutes after role assignment, then retry |
| `session_not_ready` error | Container startup failure | Check `azd ai agent monitor --session-id <id>` for crash logs |
| Tool schemas rejected by OpenAI | MCP server returns malformed schemas | Sanitize schemas — add empty `properties` to `object` types missing them |

### Validating a Toolbox Endpoint

After creating a toolbox, confirm that the MCP endpoint works:

1. Call `tools/list` — should return the tool list without errors.
2. Call `tools/call` on a specific tool — confirms end-to-end MCP protocol behavior.

The full MCP endpoint URL has the form:

```
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<toolbox-name>/versions/<version-number>/mcp?api-version=v1
```

> **Note:** The `?api-version=v1` query parameter is required. Omitting it returns HTTP 400.

## Troubleshooting Multi-Tool Toolbox Creation

When creating a toolbox with multiple tools, Foundry validates tool identity.

### Symptom

You may see this error when combining multiple tools that do not expose a unique identifier field:

`(invalid_payload) Multiple tools without identifiers found. All tools except a single tool must have unique identifiers ('name' or 'server_label').`

### Why This Happens

- Some tool types do not accept `name` or `server_label` in toolbox definitions (for example `file_search`, `web_search`, `azure_ai_search`, `code_interpreter`).
- Foundry allows only one such unnamed tool in a single toolbox payload.

### Fix Pattern

- Keep at most one unnamed tool per toolbox.
- If you need multiple tools in one toolbox, add tools that provide identifiers, such as `MCPTool` with a unique `server_label`.

The combinations in `sample_toolboxes_crud.py` use this pattern:

- `multi-filesearch-codeinterp`: `FileSearchTool` + `MCPTool(server_label=...)`
- `multi-websearch-codeinterp`: `WebSearchTool` + `MCPTool(server_label=...)`
- `multi-aisearch-codeinterp`: `AzureAISearchTool` + `MCPTool(server_label=...)`

### Quick Validation

After creating a toolbox sample, validate the MCP endpoint with:

1. `tools/list`
2. `tools/call`

This confirms both toolbox provisioning and MCP protocol behavior end-to-end.

## Source Data Patterns by Tool Type for Citation

Different toolbox tools return citation/source data in different shapes inside the `tools/call` response.

### Azure AI Search

Citation data is in `result.structuredContent.documents[]`:

| Field | Description |
|-------|-------------|
| `title` | Display label for the citation |
| `url` | Clickable source link |
| `id` | Stable source identifier |
| `score` | Relevance score |
| `knowledgeSourceIndex` | Knowledge source grouping/index |

- `result.structuredContent.summary` — explains retrieval outcome (e.g. number of retrieved docs)
- `result.structuredContent.additionalProperties.num_docs_retrieved` — useful for diagnostics
- `result.content[]` — tool text output; this is response text, **not** the authoritative citation list

### File Search

Chunk metadata is embedded in the `tools/call` response as `〔index† filename† file_id〕` markers inside
`result.content[].resource.text`. Full metadata for each matched chunk is in the `_meta` block
of the same resource item:

| Field | Location | Description |
|-------|----------|-------------|
| `title` | `resource._meta.title` | Source file name |
| `file_id` | `resource._meta.file_id` | Stable identifier for the source file |
| `document_chunk_id` | `resource._meta.document_chunk_id` | Identifier for the specific chunk |
| `score` | `resource._meta.score` | Relevance score for the chunk |

Example `tools/call` response:

```json
{
  "jsonrpc": "2.0",
  "id": "fs-call-1",
  "result": {
    "content": [
      {
        "type": "resource",
        "resource": {
          "uri": "file://assistant-tvfqncbtruyffxkfewenyy/",
          "_meta": {
            "title": "mcp-test-file.txt",
            "file_id": "assistant-TVfQnCBtRuyfFxkfeweNYY",
            "document_chunk_id": "f7327b7f-5ed0-43c6-9bee-e8e9552afcb5",
            "score": 0.03333333507180214
          },
          "text": "# \u30100\u2020mcp-test-file.txt\u2020assistant-TVfQnCBtRuyfFxkfeweNYY\u3011\nContent Snippet:\nAzure OpenAI Service is a cloud service..."
        }
      }
    ]
  }
}
```

Use the `_meta` fields to build citation links or deep-link back to the source file.

### Web Search

The response is a single resource content item with the synthesized answer. URL citations are in
`result.content[].resource._meta.annotations[]`.

| Field | Location | Description |
|-------|----------|-------------|
| `text` | `resource.text` | Synthesized answer with inline Markdown source links |
| `type` | `_meta.annotations[].type` | Always `"url_citation"` |
| `url` | `_meta.annotations[].url` | Source URL |
| `title` | `_meta.annotations[].title` | Source page title |
| `start_index` / `end_index` | `_meta.annotations[].start_index` / `end_index` | Character offsets into `resource.text` where the citation appears |
| `query` | `_meta.action.query` | The search query the model issued |

Example `tools/call` response:

```json
{
  "jsonrpc": "2.0",
  "id": "ws-call-1",
  "result": {
    "_meta": {
      "tool_configuration": {
        "type": "web_search",
        "name": "web-search-default"
      }
    },
    "content": [
      {
        "type": "resource",
        "resource": {
          "uri": "about:web-search-answer",
          "mimeType": "text/plain",
          "text": "Here are the latest updates...\n\n- **GPT-image-1 Release** ([serverless-solutions.com](https://...))."
        },
        "annotations": { "audience": ["assistant"] },
        "_meta": {
          "annotations": [
            {
              "type": "url_citation",
              "url": "https://www.serverless-solutions.com/blog/...",
              "title": "Microsoft Expands Azure AI Foundry with Powerful New OpenAI Models",
              "start_index": 741,
              "end_index": 879
            }
          ],
          "action": {
            "type": "search",
            "query": "Azure OpenAI service updates 2026",
            "queries": ["Azure OpenAI service updates 2026"]
          },
          "response_id": "resp_001fcebcc300..."
        }
      }
    ],
    "isError": false
  }
}
```

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

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
