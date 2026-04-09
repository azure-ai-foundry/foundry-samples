# Python Toolbox Samples

This directory consolidates the Python toolbox-related samples into one place.

## Implementations

| Folder | Description |
|------|-------------|
| `langgraph/` | LangGraph-based Responses agent wired to Azure AI Foundry toolbox MCP |
| `maf/` | Microsoft Agent Framework implementation using toolbox MCP |
| `copilot-sdk/` | GitHub Copilot SDK agent with toolbox MCP and local skills |

## Supported Toolbox Tools

Canonical tool/auth definitions are centralized in [SUPPORTED_TOOLBOX_TOOLS.md](./SUPPORTED_TOOLBOX_TOOLS.md).

| Sample | Uses Shared Toolbox Tools Component |
|--------|:---:|
| `langgraph/` | ✅ |
| `maf/` | ✅ |
| `copilot-sdk/` | ✅ |

### Sample Capabilities

| Capability | `langgraph/` | `maf/` | `copilot-sdk/` |
|-----------|:---:|:---:|:---:|
| Multi-turn conversation | ✅ | ✅ | ✅ |
| Streaming (SSE) | ✅ | ✅ | ✅ |
| OAuth consent handling | ✅ | ❌ | ❌ |
| Tool schema sanitization | ✅ | ❌ | ❌ |


## Source Lineage

These folders are organized copies of the existing samples from the repo:

- `langgraph/` from `samples/python/agentserver-responses/toolbox/`
- `maf/` from `samples/python/agentserver-responses/toolbox-agentframework/`
- `copilot-sdk/` from `samples/python/copilot-sdk-toolbox/`

The original folders remain in place so existing docs, tests, and workflows are not disrupted.

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

## Azure AI Search Citation Pattern

When calling the Azure AI Search tool through toolbox MCP, citation data is returned as document metadata in `tools/call` output.

### Where to Find Citation Data

- Top-level location: `result.structuredContent.documents[]`
- Per-document citation fields:
	- `title` (display label)
	- `url` (clickable source link)
	- `id` (stable source identifier)
	- `score` (relevance score)
	- `knowledgeSourceIndex` (knowledge source grouping/index)

### Important Note

- You should not expect a dedicated `citations` array for this tool output.
- Treat each object in `structuredContent.documents` as one citation entry.

### What Else Is Useful

- `result.structuredContent.summary` explains retrieval outcome (for example, number of retrieved docs).
- `result.structuredContent.additionalProperties.num_docs_retrieved` is useful for diagnostics.
- `result.content[]` contains tool text output; this is response text, not the authoritative citation list.
