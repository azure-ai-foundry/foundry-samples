<!-- Begin standard disclaimer — do not modify -->
**IMPORTANT!** All samples and other resources made available in this GitHub repository ("samples") are designed to assist in accelerating development of agents, solutions, and agent workflows for various scenarios. Review all provided resources and carefully test output behavior in the context of your use case. AI responses may be inaccurate and AI actions should be monitored with human oversight. Learn more in the transparency documents for [Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/responsible-ai/agents/transparency-note) and [Agent Framework](https://github.com/microsoft/agent-framework/blob/main/TRANSPARENCY_FAQ.md).

Agents, solutions, or other output you create may be subject to legal and regulatory requirements, may require licenses, or may not be suitable for all industries, scenarios, or use cases. By using any sample, you are acknowledging that any output created using those samples are solely your responsibility, and that you will comply with all applicable laws, regulations, and relevant safety standards, terms of service, and codes of conduct.

Third-party samples contained in this folder are subject to their own designated terms, and they have not been tested or verified by Microsoft or its affiliates.

Microsoft has no responsibility to you or others with respect to any of these samples or any resulting output.
<!-- End standard disclaimer -->

# .NET Toolbox CRUD Sample

SDK sample for creating, listing, updating, and deleting Microsoft Foundry **Toolbox**
resources using .NET.

This is the .NET counterpart to the Python sample at
[`../../python/toolbox/sample_toolboxes_crud.py`](../../python/toolbox/sample_toolboxes_crud.py).

## What This Sample Does

`ToolboxesCrud/Program.cs` demonstrates the full toolbox lifecycle via the
`Azure.AI.Projects` SDK:

- **Create** toolbox versions with different tool types
- **List** all toolboxes and their versions
- **Get** a specific toolbox or version
- **Update** a toolbox (promote a version to default)
- **Delete** toolbox versions and entire toolboxes
- **Validate** a live toolbox via MCP `tools/list` and `tools/call`

Tool types demonstrated:
- MCP (no-auth, key-auth, OAuth, filtered tool list)
- Code Interpreter
- File Search
- Azure AI Search
- Web Search
- OpenAPI (no-auth, project-connection auth)
- A2A (agent-to-agent)
- Multi-tool combinations

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [Microsoft Foundry](https://ai.azure.com) project
- Azure CLI installed and logged in:

  ```bash
  az login
  ```

- Your account needs at least **Contributor** role on the Foundry project.

## Quick Start

### 1. Set environment variables

**Bash / macOS / Linux:**
```bash
# Required
export FOUNDRY_PROJECT_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project>

# Optional — needed for key-based and OAuth MCP scenarios
export MCP_CONNECTION_ID=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<name>
export MCP_OAUTH_CONNECTION_ID=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<oauth-name>

# Optional — needed for File Search scenario
export VECTOR_STORE_ID=vs_...

# Optional — needed for Azure AI Search scenario
export AI_SEARCH_CONNECTION_ID=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<search-name>
export AI_SEARCH_INDEX_NAME=my-index

# Optional — needed for OpenAPI with connection auth scenario
export OPENAPI_CONNECTION_ID=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<api-name>

# Optional — needed for A2A scenario
export A2A_BASE_URL=https://<remote-agent-host>
export A2A_CONNECTION_ID=/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<a2a-name>
```

**PowerShell (Windows):**
```powershell
# Required
$env:FOUNDRY_PROJECT_ENDPOINT = "https://<account>.services.ai.azure.com/api/projects/<project>"

# Optional — needed for key-based and OAuth MCP scenarios
$env:MCP_CONNECTION_ID = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<name>"
$env:MCP_OAUTH_CONNECTION_ID = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<oauth-name>"

# Optional — needed for File Search scenario
$env:VECTOR_STORE_ID = "vs_..."

# Optional — needed for Azure AI Search scenario
$env:AI_SEARCH_CONNECTION_ID = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<search-name>"
$env:AI_SEARCH_INDEX_NAME = "my-index"

# Optional — needed for OpenAPI with connection auth scenario
$env:OPENAPI_CONNECTION_ID = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<api-name>"

# Optional — needed for A2A scenario
$env:A2A_BASE_URL = "https://<remote-agent-host>"
$env:A2A_CONNECTION_ID = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<a2a-name>"
```

### 2. Run the sample

Run all samples:
```bash
cd ToolboxesCrud
dotnet run -- all
```

Run a specific sample:
```bash
dotnet run -- mcp-noauth        # MCP (no auth)
dotnet run -- mcp-keyauth       # MCP (key auth)
dotnet run -- mcp-oauth         # MCP (OAuth)
dotnet run -- mcp-filtered      # MCP (filtered tools)
dotnet run -- code-interpreter  # Code Interpreter
dotnet run -- filesearch        # File Search
dotnet run -- azure-ai-search   # Azure AI Search
dotnet run -- websearch         # Web Search
dotnet run -- openapi-noauth    # OpenAPI (no auth)
dotnet run -- openapi-conn      # OpenAPI (project connection auth)
dotnet run -- a2a               # A2A (agent-to-agent)
dotnet run -- multi             # Multi-tool (MCP + MCP)
dotnet run -- list              # List all toolboxes
```

## Getting Your `FOUNDRY_PROJECT_ENDPOINT`

1. Go to [ai.azure.com](https://ai.azure.com) and open your project.
2. Navigate to **Settings** → **Project details**.
3. Copy the **Project endpoint** — it looks like:

   ```
   https://<account>.services.ai.azure.com/api/projects/<project>
   ```

## Getting a Connection ID

Connection IDs are required for MCP tools that use key-based or OAuth authentication.

1. In [ai.azure.com](https://ai.azure.com), go to your project → **Settings** → **Connected resources**.
2. Click an existing connection to view its details and resource ID.
3. The connection resource ID follows the pattern:

   ```
   /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/<name>
   ```

To create a new connection, go to **Settings** → **Connected resources** → **New connection**.

## Expected Output

The sample prints each operation with its result:

```
=== Creating toolbox: mcp-noauth ===
  tools/list → 5 tool(s)
    - fetch
    - search
    ...

=== Creating toolbox: file-search ===
  Created version 1

=== Listing all toolboxes ===
  mcp-noauth (default: 1)
  file-search (default: 1)
  ...

=== Deleting all toolboxes ===
  Deleted mcp-noauth
  Deleted file-search
```

## Troubleshooting

### `401 Unauthorized`

Run `az login` and ensure your account has **Contributor** role on the Foundry project.

### `404 Not Found` on toolbox MCP endpoint

Ensure the URL includes `?api-version=v1`. Two URL forms are supported:
```
# Latest version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/mcp?api-version=v1

# Pinned to a specific version:
https://<account>.services.ai.azure.com/api/projects/<project>/toolboxes/<name>/versions/<version>/mcp?api-version=v1
```

### `InvalidOperationException: Set FOUNDRY_PROJECT_ENDPOINT`

The `FOUNDRY_PROJECT_ENDPOINT` environment variable is not set. Follow Step 1 above.

### NuGet restore fails

The `nuget.config` file references a private feed required during preview.
Ensure you have been granted access to the feed.

## SDK Reference

The sample uses these NuGet packages:
- `Azure.AI.Projects` — `AIProjectClient`, `AgentAdministrationClient`, `AgentToolboxes`
- `Azure.AI.Projects.Agents` — toolbox tool types
- `Azure.Identity` — `DefaultAzureCredential`

API surface used:
```csharp
var toolboxClient = projectClient.AgentAdministrationClient.GetAgentToolboxes();
await toolboxClient.CreateToolboxVersionAsync(name, tools, description, metadata);
await toolboxClient.GetToolboxAsync(name);
await toolboxClient.GetToolboxVersionAsync(name, version);
await toolboxClient.GetToolboxesAsync();
await toolboxClient.UpdateToolboxAsync(name, defaultVersion: version);
await toolboxClient.DeleteToolboxVersionAsync(name, version);
await toolboxClient.DeleteToolboxAsync(name);
```

## Contributing

This project welcomes contributions and suggestions.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.
