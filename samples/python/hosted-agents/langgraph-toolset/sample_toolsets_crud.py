"""
Comprehensive SDK samples for Azure AI Foundry Toolsets CRUD operations.

All tool types demonstrated with correct azure-ai-projects SDK models:
  - MCPTool (no-auth, key-auth via project_connection_id, OAuth via project_connection_id)
  - OpenApiTool (anonymous auth, project-connection auth)
  - A2APreviewTool (agent-to-agent)
  - FileSearchTool
  - AzureAISearchTool (with AzureAISearchToolResource + AzureAISearchIndex)
  - CodeInterpreterTool
  - Multi-tool (combining multiple tools in one toolset)

Prerequisites:
  pip install azure-ai-projects azure-identity python-dotenv
  Set environment variables in .env (see bottom of file for required vars).

SDK model field reference (verified via introspection):
  MCPTool:       server_label, server_url, project_connection_id, allowed_tools, headers
                 (NO 'name' field, NO 'auth' field — use project_connection_id directly)
  OpenApiTool:   openapi={name, spec, auth}
                 auth types: OpenApiAnonymousAuthDetails()
                             OpenApiProjectConnectionAuthDetails: security_scheme=OpenApiProjectConnectionSecurityScheme(project_connection_id=...)
                             OpenApiManagedAuthDetails(security_scheme=...)
  AzureAISearchTool:          azure_ai_search=AzureAISearchToolResource(indexes=[AzureAISearchIndex(index_name=...)])
  FileSearchTool:             vector_store_ids=[...]
  CodeInterpreterTool:        (no params)
  A2APreviewTool:             name, project_connection_id, description
"""

import os
import sys
from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    MCPTool,
    OpenApiTool,
    A2APreviewTool,
    FileSearchTool,
    AzureAISearchTool,
    AzureAISearchToolResource,
    AzureAISearchIndex,
    CodeInterpreterTool,
    OpenApiAnonymousAuthDetails,
    OpenApiProjectConnectionAuthDetails,
    OpenApiProjectConnectionSecurityScheme,
)

load_dotenv()

ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]

credential = DefaultAzureCredential()
client = AIProjectClient(endpoint=ENDPOINT, credential=credential)


# ---------------------------------------------------------------------------
# 1. MCP — No Auth (public server, e.g. gitmcp.io)
# ---------------------------------------------------------------------------
def sample_mcp_no_auth():
    """Create a toolset with a public MCP server (no auth required)."""
    toolset = client.beta.toolsets.create(
        name="mcp-noauth-sample",
        tools=[
            MCPTool(
                server_label="gitmcp",
                server_url="https://gitmcp.io/Azure-Samples/agent-openai-python-prompty",
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 2. MCP — Key Auth (e.g. GitHub MCP with PAT stored in a project connection)
# ---------------------------------------------------------------------------
def sample_mcp_key_auth():
    """Create a toolset with an MCP server that requires key-based auth.

    Auth is specified via project_connection_id directly on MCPTool —
    there is no separate auth wrapper object.
    """
    toolset = client.beta.toolsets.create(
        name="mcp-keyauth-sample",
        tools=[
            MCPTool(
                server_label="github",
                server_url="https://api.githubcopilot.com/mcp",
                project_connection_id=os.environ["MCP_CONNECTION_ID"],
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 3. MCP — OAuth (e.g. GitHub MCP with OAuth connection)
# ---------------------------------------------------------------------------
def sample_mcp_oauth():
    """Create a toolset with an MCP server using OAuth connection.

    Uses project_connection_id pointing to an OAuth-type connection.
    At runtime, tools/call may return CONSENT_REQUIRED with a consent
    URL that the user must visit to authorize.
    """
    toolset = client.beta.toolsets.create(
        name="mcp-oauth-sample",
        tools=[
            MCPTool(
                server_label="github-oauth",
                server_url="https://api.githubcopilot.com/mcp",
                project_connection_id=os.environ["MCP_OAUTH_CONNECTION_ID"],
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 4. MCP — With allowed_tools filter and custom headers
# ---------------------------------------------------------------------------
def sample_mcp_filtered():
    """Create a toolset with filtered tools and custom headers."""
    toolset = client.beta.toolsets.create(
        name="mcp-filtered-sample",
        tools=[
            MCPTool(
                server_label="github-filtered",
                server_url="https://api.githubcopilot.com/mcp",
                project_connection_id=os.environ["MCP_CONNECTION_ID"],
                allowed_tools=["search_repositories", "get_file_contents"],
                headers={"Accept": "application/json"},
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 5. OpenAPI — No Auth (anonymous)
# ---------------------------------------------------------------------------
def sample_openapi_no_auth():
    """Create a toolset with a public OpenAPI spec (anonymous auth)."""
    spec = {
        "openapi": "3.0.0",
        "info": {"title": "JSON Placeholder", "version": "1.0"},
        "servers": [{"url": "https://jsonplaceholder.typicode.com"}],
        "paths": {
            "/posts/{id}": {
                "get": {
                    "operationId": "getPost",
                    "summary": "Get a post by ID",
                    "parameters": [
                        {
                            "name": "id",
                            "in": "path",
                            "required": True,
                            "schema": {"type": "integer"},
                        }
                    ],
                    "responses": {"200": {"description": "A post object"}},
                }
            }
        },
    }
    toolset = client.beta.toolsets.create(
        name="openapi-noauth-sample",
        tools=[
            OpenApiTool(
                openapi={
                    "name": "jsonplaceholder",
                    "spec": spec,
                    "auth": OpenApiAnonymousAuthDetails(),
                }
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 6. OpenAPI — With Project Connection Auth
# ---------------------------------------------------------------------------
def sample_openapi_with_connection():
    """Create a toolset with an OpenAPI spec that requires API key auth.

    Uses OpenApiProjectConnectionAuthDetails with a security_scheme
    (OpenApiProjectConnectionSecurityScheme) that contains the
    project_connection_id referencing the stored API key.
    """
    spec = {
        "openapi": "3.0.1",
        "info": {"title": "TripAdvisor API", "version": "1.0"},
        "servers": [{"url": "https://api.content.tripadvisor.com/api/v1"}],
        "paths": {
            "/location/search": {
                "get": {
                    "operationId": "searchLocations",
                    "summary": "Search for locations",
                    "parameters": [
                        {
                            "name": "searchQuery",
                            "in": "query",
                            "required": True,
                            "schema": {"type": "string"},
                        },
                        {
                            "name": "language",
                            "in": "query",
                            "schema": {"type": "string", "default": "en"},
                        },
                    ],
                    "responses": {"200": {"description": "Search results"}},
                    "security": [{"apiKeyAuth": []}],
                }
            }
        },
        "components": {
            "securitySchemes": {
                "apiKeyAuth": {
                    "type": "apiKey",
                    "name": "key",
                    "in": "query",
                }
            }
        },
    }
    toolset = client.beta.toolsets.create(
        name="openapi-tripadvisor-sample",
        tools=[
            OpenApiTool(
                openapi={
                    "name": "tripadvisor",
                    "spec": spec,
                    "auth": OpenApiProjectConnectionAuthDetails(
                        security_scheme=OpenApiProjectConnectionSecurityScheme(
                            project_connection_id=os.environ["TRIPADVISOR_CONNECTION_ID"],
                        ),
                    ),
                }
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 7. A2A — Agent-to-Agent
# ---------------------------------------------------------------------------
def sample_a2a():
    """Create a toolset with an agent-to-agent tool."""
    toolset = client.beta.toolsets.create(
        name="a2a-sample",
        tools=[
            A2APreviewTool(
                name="helper-agent",
                project_connection_id=os.environ.get("A2A_CONNECTION_ID", ""),
                description="A helper agent for delegating tasks",
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 8. File Search
# ---------------------------------------------------------------------------
def sample_file_search():
    """Create a toolset with vector file search capability."""
    toolset = client.beta.toolsets.create(
        name="filesearch-sample",
        tools=[
            FileSearchTool(
                vector_store_ids=[os.environ.get("VECTOR_STORE_ID", "vs_placeholder")],
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 9. Azure AI Search
# ---------------------------------------------------------------------------
def sample_azure_ai_search():
    """Create a toolset with Azure AI Search index.

    Uses AzureAISearchToolResource containing a list of AzureAISearchIndex
    objects, each specifying an index_name.
    """
    toolset = client.beta.toolsets.create(
        name="aisearch-sample",
        tools=[
            AzureAISearchTool(
                azure_ai_search=AzureAISearchToolResource(
                    indexes=[
                        AzureAISearchIndex(
                            index_name=os.environ["AI_SEARCH_INDEX_NAME"],
                        )
                    ]
                )
            )
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 10. Code Interpreter
# ---------------------------------------------------------------------------
def sample_code_interpreter():
    """Create a toolset with sandboxed code execution."""
    toolset = client.beta.toolsets.create(
        name="codeinterp-sample",
        tools=[CodeInterpreterTool()],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 11. Multi-Tool (combine multiple MCP servers in one toolset)
# ---------------------------------------------------------------------------
def sample_multi_tool():
    """Create a toolset with multiple tools.

    Each MCP tool MUST have a unique server_label.
    """
    toolset = client.beta.toolsets.create(
        name="multi-tool-sample",
        tools=[
            MCPTool(
                server_label="gitmcp",
                server_url="https://gitmcp.io/Azure-Samples/agent-openai-python-prompty",
            ),
            MCPTool(
                server_label="github",
                server_url="https://api.githubcopilot.com/mcp",
                project_connection_id=os.environ["MCP_CONNECTION_ID"],
            ),
        ],
    )
    print(f"Created: {toolset.id}")
    return toolset


# ---------------------------------------------------------------------------
# 12. List all toolsets
# ---------------------------------------------------------------------------
def sample_list_all():
    """List all toolsets in the project."""
    toolsets = client.beta.toolsets.list()
    for ts in toolsets:
        print(f"  {ts.id}  {ts.name}")
    return toolsets


# ---------------------------------------------------------------------------
# Runner
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    samples = {
        "mcp-noauth": sample_mcp_no_auth,
        "mcp-keyauth": sample_mcp_key_auth,
        "mcp-oauth": sample_mcp_oauth,
        "mcp-filtered": sample_mcp_filtered,
        "openapi-noauth": sample_openapi_no_auth,
        "openapi-conn": sample_openapi_with_connection,
        "a2a": sample_a2a,
        "filesearch": sample_file_search,
        "aisearch": sample_azure_ai_search,
        "codeinterp": sample_code_interpreter,
        "multi": sample_multi_tool,
        "list": sample_list_all,
    }

    if len(sys.argv) < 2 or sys.argv[1] not in samples:
        print(f"Usage: python {sys.argv[0]} <sample>")
        print(f"Samples: {', '.join(samples.keys())}")
        sys.exit(1)

    result = samples[sys.argv[1]]()
    if hasattr(result, "id"):
        print(f"\nToolset ID: {result.id}")
        if input("Delete? [y/N] ").lower() == "y":
            client.beta.toolsets.delete(result.id)
            print("Deleted.")
