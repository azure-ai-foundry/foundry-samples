# Supported Toolbox Scenarios

All hosted-agents toolbox samples can be configured for any of these 14 scenarios. For each scenario, create an `agent.manifest.yaml` file using the example provided below, then pass it to `azd ai agent init -m <manifest-file>`.

---

## 1. Web Search

No connection or secrets required. The simplest toolbox scenario.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-web-search
displayName: "Web Search Toolbox Agent"
description: >
  Hosted agent with a Bing web search toolbox. The simplest toolbox
  scenario — no connections or secrets required.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties: []
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: toolbox
    name: agent-tools
    tools:
      - type: web_search
```

---

## 2. File Search

Requires a vector store in the same Foundry project. Prompted parameter: `file_search_vector_store_id`.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-file-search
displayName: "File Search Toolbox Agent"
description: >
  Hosted agent with a File Search toolbox backed by an Azure AI
  Foundry vector store. The vector store must exist in the same project.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: file_search_vector_store_id
      secret: false
      description: Vector store ID from the same Foundry project (e.g. vs_xxxxxxxxxxxx)
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: toolbox
    name: agent-tools
    tools:
      - type: file_search
        vector_store_ids:
          - "{{ file_search_vector_store_id }}"
```

---

## 3. Code Interpreter

No secrets required. Executes Python code in a sandboxed environment.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-code-interpreter
displayName: "Code Interpreter Toolbox Agent"
description: >
  Hosted agent with a Code Interpreter toolbox. Executes Python
  code in a sandboxed environment via toolbox in Microsoft Foundry.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties: []
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: toolbox
    name: agent-tools
    tools:
      - type: code_interpreter
```

---

## 4. MCP Key-Auth (GitHub)

Prompted parameter: `github_pat` (GitHub Personal Access Token, injected as Bearer token).

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-mcp-keyauth
displayName: "GitHub MCP Key-Auth Toolbox Agent"
description: >
  Hosted agent with a GitHub MCP toolbox using key-based authentication
  (GitHub PAT injected as Bearer token).
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: github_pat
      secret: true
      description: GitHub Personal Access Token (classic ghp_... or fine-grained github_pat_...)
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: github-mcp-conn
    category: RemoteTool
    authType: CustomKeys
    target: https://api.githubcopilot.com/mcp
    credentials:
      type: CustomKeys
      keys:
        Authorization: "Bearer {{ github_pat }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: mcp
        server_label: github
        project_connection_id: github-mcp-conn
```

---

## 5. MCP No-Auth

Prompted parameter: `mcp_endpoint` (URL of the public MCP server). No credentials needed.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-mcp-noauth
displayName: "Public MCP No-Auth Toolbox Agent"
description: >
  Hosted agent connected to a public MCP server that requires no
  authentication. The server URL is proxied by toolbox in Microsoft Foundry.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: mcp_endpoint
      secret: false
      description: URL of the public MCP server (e.g. https://gitmcp.io/Azure/azure-rest-api-specs)
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: toolbox
    name: agent-tools
    tools:
      - type: mcp
        server_label: noauthmcp
        server_url: "{{ mcp_endpoint }}"
```

---

## 6. MCP OAuth (Managed Connector)

No secrets required — Foundry manages the OAuth app registration. First invocation returns a consent URL (MCP code `-32006`).

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-mcp-oauth-managed
displayName: "MCP OAuth2 Managed Connector Toolbox Agent"
description: >
  Hosted agent with a GitHub MCP toolbox using Microsoft Foundry's
  managed OAuth connector. No client credentials needed — Foundry handles
  the OAuth app registration. First invocation triggers a consent flow.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties: []
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: github-oauth-conn
    category: RemoteTool
    authType: OAuth2
    target: https://api.githubcopilot.com/mcp
    connectorName: foundrygithubmcp
    credentials:
      type: OAuth2
      clientId: managed
      clientSecret: managed
  - kind: toolbox
    name: agent-tools
    tools:
      - type: mcp
        server_label: github
        project_connection_id: github-oauth-conn
```

---

## 7. MCP OAuth (Custom App)

Prompted parameters: `your_client_id`, `your_client_secret` (OAuth2 app registration). First invocation returns a consent URL (MCP code `-32006`).

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-mcp-oauth-custom
displayName: "MCP OAuth2 Custom App Registration Toolbox Agent"
description: >
  Hosted agent with a GitHub MCP toolbox using a bring-your-own
  OAuth2 app registration. First invocation triggers a consent flow.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: your_client_id
      secret: false
      description: OAuth2 client ID from your app registration
    - name: your_client_secret
      secret: true
      description: OAuth2 client secret from your app registration
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: github-oauth-custom-conn
    category: RemoteTool
    authType: OAuth2
    target: https://api.githubcopilot.com/mcp
    credentials:
      type: OAuth2
      clientId: "{{ your_client_id }}"
      clientSecret: "{{ your_client_secret }}"
    authorizationUrl: "https://github.com/login/oauth/authorize"
    tokenUrl: "https://github.com/login/oauth/access_token"
    refreshUrl: "https://github.com/login/oauth/access_token"
    scopes:
      - repo
      - read:user
  - kind: toolbox
    name: agent-tools
    tools:
      - type: mcp
        server_label: github
        project_connection_id: github-oauth-custom-conn
```

---

## 8. MCP Agent Identity

Prompted parameters: `entra_audience`, `mcp_target_url`. Assign an RBAC role to the agent's managed identity on the target MCP server before deploying.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-mcp-agent-identity
displayName: "MCP Agent Identity Toolbox Agent"
description: >
  Hosted agent with an MCP toolbox using Microsoft Foundry's Agentic
  Identity (agent managed identity) for Entra ID authentication to the MCP server.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: entra_audience
      secret: false
      description: Entra ID audience for the target MCP server
    - name: mcp_target_url
      secret: false
      description: URL of the MCP server that accepts agent identity tokens
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: language-mcp
    category: RemoteTool
    authType: AgenticIdentity
    audience: "{{ entra_audience }}"
    target: "{{ mcp_target_url }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: mcp
        server_label: language-mcp
        project_connection_id: language-mcp
```

---

## 9. Azure AI Search

Prompted parameters: `ai_search_endpoint`, `ai_search_key`, `ai_search_index_name`.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-ai-search
displayName: "Azure AI Search Toolbox Agent"
description: >
  Hosted agent with an Azure AI Search toolbox. Queries an existing
  search index via toolbox proxy in Microsoft Foundry.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: ai_search_endpoint
      secret: false
      description: Azure AI Search service endpoint (e.g. https://my-search.search.windows.net/)
    - name: ai_search_key
      secret: true
      description: Azure AI Search admin key
    - name: ai_search_index_name
      secret: false
      description: Name of the search index to query
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: aisearch-conn
    category: CognitiveSearch
    authType: ApiKey
    target: "{{ ai_search_endpoint }}"
    credentials:
      type: ApiKey
      key: "{{ ai_search_key }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: azure_ai_search
        index_name: "{{ ai_search_index_name }}"
        project_connection_id: aisearch-conn
```

---

## 10. A2A (Agent-to-Agent)

Prompted parameter: `a2a_agent_endpoint` (URL of the remote A2A-compatible agent).

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-a2a
displayName: "Agent-to-Agent (A2A) Toolbox Agent"
description: >
  Hosted agent with an Agent-to-Agent (A2A) toolbox. Calls a remote
  A2A-compatible agent endpoint via toolbox proxy in Microsoft Foundry.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: a2a_agent_endpoint
      secret: false
      description: URL of the remote A2A-compatible agent endpoint (e.g. https://my-agent.azurecontainerapps.io)
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: a2a-conn
    category: RemoteA2A
    authType: None
    target: "{{ a2a_agent_endpoint }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: a2a_preview
        project_connection_id: a2a-conn
```

---

## 11. Bing Custom Search

Prompted parameters: `bing_api_key`, `bing_resource_id`, `bing_custom_instance`.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-bing-custom-search
displayName: "Bing Custom Search Toolbox Agent"
description: >
  Hosted agent with a Bing Custom Search toolbox. Uses a
  GroundingWithCustomSearch connection for scoped web search.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: bing_api_key
      secret: true
      description: Bing Search API key
    - name: bing_resource_id
      secret: false
      description: ARM resource ID of your Bing account
    - name: bing_custom_instance
      secret: false
      description: Bing Custom Search instance name
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: bing-custom-conn
    category: GroundingWithCustomSearch
    authType: ApiKey
    target: https://api.bing.microsoft.com/
    credentials:
      type: ApiKey
      key: "{{ bing_api_key }}"
    metadata:
      ResourceId: "{{ bing_resource_id }}"
      type: bing_custom_search_preview
  - kind: toolbox
    name: agent-tools
    tools:
      - type: web_search
        custom_search_configuration:
          instance_name: "{{ bing_custom_instance }}"
          project_connection_id: bing-custom-conn
```

---

## 12. OpenAPI Key-Auth

Prompted parameter: `tripadvisor_api_key`. Replace the spec and connection with your own OpenAPI service.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-openapi-keyauth
displayName: "OpenAPI Key-Auth Toolbox Agent"
description: >
  Hosted agent with an OpenAPI toolbox using key-based auth.
  Uses TripAdvisor Content API as an example — replace the spec and
  connection with your own OpenAPI service.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: tripadvisor_api_key
      secret: true
      description: TripAdvisor Content API key (replace with your own OpenAPI service key)
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: tripadvisor-conn
    category: CustomKeys
    authType: CustomKeys
    target: https://api.content.tripadvisor.com
    credentials:
      type: CustomKeys
      keys:
        key: "{{ tripadvisor_api_key }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: openapi
        openapi:
          name: tripadvisor
          spec:
            openapi: "3.0.1"
            info:
              title: "TripAdvisor API"
              version: "1.0"
            servers:
              - url: https://api.content.tripadvisor.com/api/v1
            paths:
              /location/search:
                get:
                  operationId: searchLocations
                  parameters:
                    - name: searchQuery
                      in: query
                      required: true
                      schema:
                        type: string
                    - name: key
                      in: query
                      required: true
                      schema:
                        type: string
                  responses:
                    "200":
                      description: OK
          auth:
            type: connection_auth
            connection_id: tripadvisor-conn
```

---

## 13. MCP OAuth (Entra Passthrough)

Prompted parameters: `entra_audience`, `entra_mcp_target`. Foundry proxies the caller's Entra identity to the downstream MCP server.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-mcp-entra-passthrough
displayName: "MCP Entra Token Passthrough Toolbox Agent"
description: >
  Hosted agent with an MCP toolbox that uses Entra token passthrough.
  Microsoft Foundry proxies the caller's Entra identity to the downstream MCP server.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: entra_audience
      secret: false
      description: Entra ID audience for the target MCP server
    - name: entra_mcp_target
      secret: false
      description: URL of the MCP server that accepts Entra user tokens
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: entra-passthrough-conn
    category: RemoteTool
    authType: UserEntraToken
    audience: "{{ entra_audience }}"
    target: "{{ entra_mcp_target }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: mcp
        server_label: outlook-mail
        project_connection_id: entra-passthrough-conn
```

---

## 14. Multi-Tool Toolbox

Prompted parameter: `github_pat`. Combines Bing web search and GitHub MCP in one toolbox.

**`agent.manifest.yaml`**

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/microsoft/AgentSchema/refs/heads/main/schemas/v1.0/AgentManifest.yaml
name: toolbox-hosted-multi-tool
displayName: "Multi-Tool Toolbox Agent (Web Search + GitHub MCP)"
description: >
  Hosted agent with a combined toolbox: Bing web search plus GitHub
  MCP tools via key-based auth. Demonstrates multiple tool types in one toolbox.
template:
  kind: hosted
  protocols:
    - protocol: responses
      version: 1.0.0
parameters:
  properties:
    - name: github_pat
      secret: true
      description: GitHub Personal Access Token (classic ghp_... or fine-grained github_pat_...)
resources:
  - kind: model
    id: gpt-4o
    name: AZURE_AI_MODEL_DEPLOYMENT_NAME
  - kind: connection
    name: github-mcp-conn
    category: RemoteTool
    authType: CustomKeys
    target: https://api.githubcopilot.com/mcp
    credentials:
      type: CustomKeys
      keys:
        Authorization: "Bearer {{ github_pat }}"
  - kind: toolbox
    name: agent-tools
    tools:
      - type: web_search
      - type: mcp
        server_label: github
        project_connection_id: github-mcp-conn
```
