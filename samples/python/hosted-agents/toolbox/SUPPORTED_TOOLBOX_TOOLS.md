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

Enables web grounding (Bing search).

| Parameter | Required | Description |
|-----------|----------|-------------|
| `custom_search_configuration` | No | Restrict to specific domains via Bing Custom Search |

**Domain-restricted search** (`custom_search_configuration`) requires key-based auth:

| Parameter | Required | Description |
|-----------|----------|-------------|
| `project_connection_id` | Yes | Connection to a Bing Custom Search resource |
| `instance_name` | Yes | Name of the custom search instance |

### Code Interpreter Tool

Runs Python code in a sandboxed environment for analysis, math, and chart generation.

No required parameters. Auth: N/A.

### A2A Tool (preview)

Delegates tasks to another agent via the Agent-to-Agent protocol.

| Parameter | Required | Description |
|-----------|----------|-------------|
| `name` | Yes | Logical name for the sub-agent tool |
| `project_connection_id` | Yes | Connection ID pointing to the remote agent |
| `description` | No | Human-readable description of the sub-agent capabilities |

Auth options are the same as MCP Tool.

## Notes

- All tool types are served through the same Foundry MCP gateway endpoint.
- Use [sample_toolboxes_crud.py](./sample_toolboxes_crud.py) for runnable SDK examples.
