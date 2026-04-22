# Supported Toolbox Tools

Use this file as the single source of truth for toolbox tool support and authentication across Python toolbox samples.

## Tool Support Matrix

| Toolbox Tool Type | Supported Auth |
|-------------------|----------------|
| **MCP Tool** | Key-based, OAuth (identity passthrough), Entra ID (agent identity), Entra ID (managed identity) |
| **File Search Tool** | N/A |
| **OpenAPI Tool** | Anonymous, Key-based, Entra ID (managed identity on Foundry project) |
| **Azure AI Search Tool** | Key-based, Entra ID (agent identity), Entra ID (managed identity) |
| **Web Search Tool** | Anonymous, Key-based (domain-restricted via Bing Custom Search) |
| **Code Interpreter Tool** | N/A |
| **A2A Tool** (preview) | Key-based, OAuth (identity passthrough), Entra ID |

## Detailed Tool Definitions

### MCP Tool

Connects to a remote Model Context Protocol server.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `server_label` | Yes | Unique label for this MCP server within the toolbox |
| `server_url` | Yes | HTTPS URL of the MCP server |
| `project_connection_id` | Yes | Project connection for auth (key, OAuth, Entra) |
| `allowed_tools` | No | List of tool names to expose (filters the full set) |
| `headers` | No | Extra HTTP headers sent with every MCP request |

**Auth options:**

| Mode | User context preserved | How to configure |
|------|------------------------|------------------|
| Key-based | No | Set `project_connection_id` to a Custom Keys connection holding the API key or PAT |
| OAuth identity passthrough | Yes | Set `project_connection_id` to an OAuth-type connection. At runtime the agent returns an `oauth_consent_request` with a consent URL |
| Entra ID - agent identity (preview) | No | Assign required roles to the agent identity on the underlying service |
| Entra ID - project managed identity | No | Assign required roles to the project managed identity |

### File Search Tool

Searches indexed files/documents via project vector stores.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `vector_store_ids` | Yes | One or more vector store IDs to search |

Auth: N/A.

### OpenAPI Tool

Calls HTTP APIs described by an OpenAPI 3.0/3.1 specification.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `openapi.name` | Yes | Logical name for the tool |
| `openapi.spec` | Yes | Inline OpenAPI spec (dict) or a reference |
| `openapi.auth` | Yes | OpenAPI auth details object |

**Auth options:**

| Mode | How to configure |
|------|------------------|
| Anonymous | `OpenApiAnonymousAuthDetails()` |
| Key-based | Use project connection-backed OpenAPI auth details |
| Entra ID - managed identity (Foundry project) | Use managed auth details backed by the project managed identity |

### Azure AI Search Tool

Grounds responses in Azure AI Search indexes.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `project_connection_id` | Yes | Resource ID of the project connection to Azure AI Search |
| `index_name` | Yes | Name of the search index (case-sensitive) |
| `top_k` | No | Number of results to return (default: 5) |
| `query_type` | No | `simple`, `vector`, `semantic`, `vector_simple_hybrid`, or `vector_semantic_hybrid` |
| `filter` | No | OData filter applied to every query |

**Auth options:**

| Mode | How to configure |
|------|------------------|
| Key-based | Store the API key in the project connection |
| Entra ID - project managed identity | Assign Search Index Data Contributor and Search Service Contributor roles |
| Entra ID - agent identity | Assign the same roles to the agent identity |

### Web Search Tool

Enables web grounding (Bing search). Two modes: anonymous (general Bing) or domain-restricted via Bing Custom Search.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `custom_search_configuration` | No | Restrict to specific domains via Bing Custom Search |

**Domain-restricted search** (`custom_search_configuration`) requires a `GroundingWithCustomSearch` connection:

| Parameter | Required | Description |
|-----------|----------|-------------|
| `project_connection_id` | Yes | Connection name (references a `GroundingWithCustomSearch` connection) |
| `instance_name` | Yes | Name of the Bing Custom Search instance (e.g., `agentdoc`) |

**Connection requirements** for Bing Custom Search:

| Field | Value |
|-------|-------|
| `category` | `GroundingWithCustomSearch` |
| `authType` | `ApiKey` |
| `target` | `https://api.bing.microsoft.com/` |
| `credentials.key` | Bing API key |
| `metadata.type` | `bing_custom_search_preview` |
| `metadata.ApiType` | `Azure` |
| `metadata.ResourceId` | ARM resource ID of the `Microsoft.Bing/accounts` resource |

> **Note:** Web Search tools only return results when called through the Responses API (which injects APIM model headers). Direct MCP `tools/call` also works via the MCP gateway when a valid model deployment exists on the project.

### Code Interpreter Tool

Runs Python code in a sandboxed environment for analysis, math, and chart generation.

No required parameters. Auth: N/A.

### A2A Tool (preview)

Delegates tasks to another agent via the Agent-to-Agent protocol. The remote agent must expose an A2A endpoint with an agent card at `/.well-known/agent.json`. Tools are auto-discovered from the agent card's skills.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | Logical name for the sub-agent tool |
| `project_connection_id` | Yes | Connection name pointing to the remote agent (`RemoteA2A` category) |
| `base_url` | No | Override the base URL from the connection (defaults to connection target) |
| `agent_card_path` | No | Override the agent card path (defaults to `/.well-known/agent.json`) |

The connection must use `category: RemoteA2A` and `metadata.type: custom_A2A`.

The MCP tool name is auto-generated as `{connection_name}.SendMessage` (e.g., `helloworld.SendMessage`).

Auth options are the same as MCP Tool.

## Notes

- All tool types are served through the same Foundry MCP gateway endpoint.
- Use [sample_toolboxes_crud.py](./sample_toolboxes_crud.py) for runnable SDK examples.
