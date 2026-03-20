# V1 to V2 Migration in Docker

This folder includes a Docker-first execution path for migrating classic assistants into versioned Foundry agents.

The container now assumes the current Agent Service split:

- `AIProjectClient` for `create_version`
- `project.get_openai_client()` for conversations and responses
- `azure-ai-projects` 2.x by default

## Build

```bash
docker build -t v1-to-v2-migration .
```

## Recommended wrapper usage

Linux or macOS:

```bash
./run-migration-docker-auth.sh --help
./run-migration-docker-auth.sh \
  --use-api \
  --production-resource nextgen-eastus \
  --production-subscription <subscription-id> \
  --production-tenant <tenant-id> \
  asst_abc123
```

Windows PowerShell:

```powershell
.\run-migration-docker-auth.ps1 --help
.\run-migration-docker-auth.ps1 `
  --use-api `
  --production-resource nextgen-eastus `
  --production-subscription <subscription-id> `
  --production-tenant <tenant-id> `
  asst_abc123
```

## Direct docker usage

```bash
docker run --rm -it \
  --network host \
  -v ~/.azure:/home/migration/.azure \
  -e AZ_TOKEN="$(az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv)" \
  -e PRODUCTION_TOKEN="$(az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv)" \
  v1-to-v2-migration \
  --use-api \
  --production-resource nextgen-eastus \
  --production-subscription <subscription-id> \
  --production-tenant <tenant-id> \
  asst_abc123
```

If you use `--project-connection-string`, set `NEED_BETA_VERSION=true` to request a prerelease SDK inside the container:

```bash
docker run --rm -it \
  --network host \
  -v ~/.azure:/home/migration/.azure \
  -e NEED_BETA_VERSION=true \
  -e AZ_TOKEN="$(az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv)" \
  -e PRODUCTION_TOKEN="$(az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv)" \
  v1-to-v2-migration \
  --project-connection-string "eastus.api.azureml.ms;<subscription-id>;my-rg;my-project" \
  --production-resource nextgen-eastus \
  --production-subscription <subscription-id> \
  --production-tenant <tenant-id>
```

## Container behavior

- Python 3.11 slim base image
- Non-root runtime user
- Azure CLI mounted for token reuse
- Stable `azure-ai-projects` 2.x from `requirements.txt`
- Optional prerelease upgrade when `NEED_BETA_VERSION=true`

## Validation flow

The containerized script performs post-migration validation with:

1. `openai.conversations.create(...)`
2. `openai.responses.create(...)`
3. `openai.conversations.items.create(...)`
4. A second `openai.responses.create(...)`

That validates both execution and context retention in the new runtime model.
