# V1 to V2 Assistant Migration Tool

This tool migrates classic Assistants or classic Foundry agents into the current Foundry Agent Service model.

The rewritten migration flow follows the current guidance:

- Agent creation uses `project.agents.create_version(...)`
- Agent definitions are explicit `PromptAgentDefinition` objects
- Runtime validation uses `project.get_openai_client()` with conversations and responses
- The tool does not create threads, runs, or legacy messages

## What Changed

The migration target is now the Foundry Agent Service runtime model:

- v1 `assistant` becomes a versioned Foundry agent
- v1 threads become conversations
- v1 runs become responses
- Agent creation and versioning stay on the project client
- Runtime execution moves to the OpenAI client returned by the project client

Historical state is not migrated. Old threads, runs, and messages remain out of scope by design.

## Prerequisites

- Docker Desktop installed and running if you use the container path
- Azure CLI installed and authenticated with `az login`
- Python 3.11+ if you run the script directly
- `azure-ai-projects>=2.0.0`

The container installs the stable 2.x SDK by default. If you use `--project-connection-string`, the wrapper can request a prerelease SDK build, but the tool also supports direct endpoint parsing when the helper API is unavailable.

## Quick Start

### Docker wrappers

Windows PowerShell:

```powershell
.\run-migration-docker-auth.ps1 --help
```

Linux or macOS:

```bash
./run-migration-docker-auth.sh --help
```

### Direct Python execution

```bash
python v1_to_v2_migration.py --help
```

## Common Scenarios

### Migrate from the legacy Assistants API

```powershell
.\run-migration-docker-auth.ps1 `
  --use-api `
  --source-tenant "72f988bf-86f1-41af-91ab-2d7cd011db47" `
  --production-resource "nextgen-eastus" `
  --production-subscription "<subscription-id>" `
  --production-tenant "<tenant-id>" `
  asst_abc123
```

```bash
./run-migration-docker-auth.sh \
  --use-api \
  --source-tenant "72f988bf-86f1-41af-91ab-2d7cd011db47" \
  --production-resource "nextgen-eastus" \
  --production-subscription "<subscription-id>" \
  --production-tenant "<tenant-id>" \
  asst_abc123
```

### Migrate from a project endpoint

```bash
./run-migration-docker-auth.sh \
  --project-endpoint "https://your-project.services.ai.azure.com/api/projects/your-project" \
  --production-resource "nextgen-eastus" \
  --production-subscription "<subscription-id>" \
  --production-tenant "<tenant-id>" \
  asst_abc123
```

### Migrate from a project connection string

```bash
./run-migration-docker-auth.sh \
  --project-connection-string "eastus.api.azureml.ms;<subscription-id>;my-rg;my-project" \
  --production-resource "nextgen-eastus" \
  --production-subscription "<subscription-id>" \
  --production-tenant "<tenant-id>" \
  asst_abc123
```

### Inject test tools after migration

```bash
./run-migration-docker-auth.sh \
  --use-api \
  --add-test-function \
  --add-test-mcp \
  --add-test-computer \
  --add-test-imagegen \
  --production-resource "nextgen-eastus" \
  --production-subscription "<subscription-id>" \
  --production-tenant "<tenant-id>" \
  asst_abc123
```

Injected test tools are appended after the original definition has been migrated, not during source parsing.

## CLI Options

Input sources:

- `--use-api`
- `--project-endpoint URL`
- `--project-connection-string STRING`
- Cosmos DB fallback via positional connection string or `COSMOS_CONNECTION_STRING`

Migration target:

- `--production-resource RESOURCE_OR_ENDPOINT`
- `--production-subscription SUBSCRIPTION_ID`
- `--production-tenant TENANT_ID`
- `--source-tenant TENANT_ID`

Optional test tools:

- `--add-test-function`
- `--add-test-mcp`
- `--add-test-computer`
- `--add-test-imagegen`
- `--add-test-azurefunction`

## Unsupported Classic Tools

Migration continues when these classic tools are present, but they are skipped with explicit warnings:

- `connected_agent`
  Recommendation: use workflows or A2A for multi-agent orchestration.
- `event_binding`
  Recommendation: no direct equivalent in the current Agent Service.
- `output_binding`
  Recommendation: use `capture_structured_outputs` for structured output capture.

The tool never drops those silently.

## Runtime Validation

After creating the new versioned agent, the tool performs a conversation-based smoke test:

1. Creates a conversation.
2. Sends a response request against the migrated agent.
3. Adds a follow-up conversation item.
4. Sends a second response request to confirm context retention.

This keeps validation aligned with the current conversations and responses model.

## Docker Behavior

- Python 3.11 base image
- Non-root execution
- Azure CLI directory mounted into the container
- Host networking enabled for local resource access
- Stable `azure-ai-projects` 2.x by default
- Optional prerelease SDK upgrade for connection-string scenarios

## Environment Variables

- `COSMOS_CONNECTION_STRING` or `COSMOS_DB_CONNECTION_STRING`
- `AGENTS_HOST`
- `AGENTS_SUBSCRIPTION`
- `AGENTS_RESOURCE_GROUP`
- `AGENTS_WORKSPACE`
- `AGENTS_API_VERSION`
- `AZ_TOKEN`
- `PRODUCTION_TOKEN`
- `PRODUCTION_PROJECT_ENDPOINT`
- `PRODUCTION_PROJECT_NAME`

`PRODUCTION_PROJECT_ENDPOINT` overrides the default endpoint construction if your Foundry project URL does not follow the default naming pattern.

## Contributor Notes

### v1 to v2 Mapping

- Assistant definition fields map into `PromptAgentDefinition`
- Names are normalized to lowercase kebab-case for versioned agent creation
- Versions are created by the Agent Service through `create_version`
- Source reading can still come from API, project endpoint, project connection string, or Cosmos DB
- Output is always the Foundry Agent Service

### Review Checklist

- No legacy threads, runs, or messages APIs in new execution paths
- `create_version` remains the only agent creation path
- Runtime validation remains conversation plus responses based
- Unsupported tools continue to log actionable warnings
- Test tool injection stays post-migration

## References

- Official migration guidance: https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/migrate
- Container notes: see `README-Docker.md`
- Change history: see `CHANGELOG.md`
