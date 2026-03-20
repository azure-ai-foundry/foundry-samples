#!/bin/bash
# Docker migration runner with automatic token authentication (Unix/Linux/macOS)
# This script handles token generation and Docker execution automatically.

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

show_usage() {
    echo "Usage: ./run-migration-docker-auth.sh [source-options] --production-resource <resource> --production-subscription <subscription-id> --production-tenant <tenant-id> <assistant-id> [more-assistant-ids]"
    echo
    echo "Source options:"
    echo "  --use-api"
    echo "  --project-endpoint <url>"
    echo "  --project-connection-string <connection-string>"
    echo "  --source-tenant <tenant-id>"
    echo
    echo "Optional test tool injection:"
    echo "  --add-test-function"
    echo "  --add-test-mcp"
    echo "  --add-test-computer"
    echo "  --add-test-imagegen"
    echo "  --add-test-azurefunction"
    echo
    echo "Examples:"
    echo "  ./run-migration-docker-auth.sh --use-api --production-resource nextgen-eastus --production-subscription <subscription-id> --production-tenant <tenant-id> asst_abc123"
    echo "  ./run-migration-docker-auth.sh --project-endpoint https://your-project.services.ai.azure.com/api/projects/your-project --production-resource nextgen-eastus --production-subscription <subscription-id> --production-tenant <tenant-id> asst_abc123"
}

echo -e "${BLUE}Running v1 to v2 assistant migration in Docker with automatic authentication${NC}"
echo "======================================================================================"

NEED_BETA_VERSION="false"
SOURCE_TENANT=""
PRODUCTION_RESOURCE=""
PRODUCTION_SUBSCRIPTION=""
PRODUCTION_TENANT=""

POSITIONAL_ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --help|-h)
            show_usage
            exit 0
            ;;
        --project-connection-string)
            NEED_BETA_VERSION="true"
            POSITIONAL_ARGS+=("$1")
            if [[ $# -gt 1 ]]; then
                POSITIONAL_ARGS+=("$2")
                shift 2
            else
                echo -e "${RED}Missing value for --project-connection-string${NC}"
                exit 1
            fi
            ;;
        --project-connection-string=*)
            NEED_BETA_VERSION="true"
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
        --source-tenant)
            if [[ $# -gt 1 ]]; then
                SOURCE_TENANT="$2"
                POSITIONAL_ARGS+=("$1" "$2")
                shift 2
            else
                echo -e "${RED}Missing value for --source-tenant${NC}"
                exit 1
            fi
            ;;
        --source-tenant=*)
            SOURCE_TENANT="${1#*=}"
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
        --production-resource)
            if [[ $# -gt 1 ]]; then
                PRODUCTION_RESOURCE="$2"
                POSITIONAL_ARGS+=("$1" "$2")
                shift 2
            else
                echo -e "${RED}Missing value for --production-resource${NC}"
                exit 1
            fi
            ;;
        --production-resource=*)
            PRODUCTION_RESOURCE="${1#*=}"
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
        --production-subscription)
            if [[ $# -gt 1 ]]; then
                PRODUCTION_SUBSCRIPTION="$2"
                POSITIONAL_ARGS+=("$1" "$2")
                shift 2
            else
                echo -e "${RED}Missing value for --production-subscription${NC}"
                exit 1
            fi
            ;;
        --production-subscription=*)
            PRODUCTION_SUBSCRIPTION="${1#*=}"
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
        --production-tenant)
            if [[ $# -gt 1 ]]; then
                PRODUCTION_TENANT="$2"
                POSITIONAL_ARGS+=("$1" "$2")
                shift 2
            else
                echo -e "${RED}Missing value for --production-tenant${NC}"
                exit 1
            fi
            ;;
        --production-tenant=*)
            PRODUCTION_TENANT="${1#*=}"
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
        *)
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
    esac
done

echo -e "${GREEN}Parsed wrapper arguments${NC}"

if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Docker is not running. Please start Docker and try again.${NC}"
    exit 1
fi
echo -e "${GREEN}Docker is running${NC}"

if ! command -v az > /dev/null 2>&1; then
    echo -e "${RED}Azure CLI not found. Install from https://docs.microsoft.com/cli/azure/${NC}"
    exit 1
fi

if ! az account show > /dev/null 2>&1; then
    echo -e "${RED}Azure CLI not authenticated. Run 'az login' first.${NC}"
    exit 1
fi

ACCOUNT_INFO=$(az account show --query "user.name" -o tsv 2>/dev/null)
echo -e "${GREEN}Azure CLI authenticated as: ${ACCOUNT_INFO}${NC}"

if [[ -z "$PRODUCTION_RESOURCE" || -z "$PRODUCTION_SUBSCRIPTION" || -z "$PRODUCTION_TENANT" ]]; then
    echo -e "${RED}Missing required production parameters.${NC}"
    echo
    show_usage
    exit 1
fi

if [[ -n "$SOURCE_TENANT" ]]; then
    echo -e "${BLUE}Generating source Azure AI token for tenant: ${SOURCE_TENANT}${NC}"
    SOURCE_TOKEN=$(az account get-access-token --tenant "$SOURCE_TENANT" --scope https://ai.azure.com/.default --query accessToken -o tsv 2>/dev/null)
else
    echo -e "${BLUE}Generating source Azure AI token...${NC}"
    SOURCE_TOKEN=$(az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv 2>/dev/null)
fi

if [[ -z "$SOURCE_TOKEN" || ${#SOURCE_TOKEN} -lt 100 ]]; then
    echo -e "${RED}Failed to generate source token or token is invalid${NC}"
    exit 1
fi
echo -e "${GREEN}Source token generated successfully (length: ${#SOURCE_TOKEN})${NC}"

echo -e "${BLUE}Production Agent Service configuration:${NC}"
echo -e "${BLUE}   Resource: ${PRODUCTION_RESOURCE}${NC}"
echo -e "${BLUE}   Subscription: ${PRODUCTION_SUBSCRIPTION}${NC}"
echo -e "${BLUE}   Tenant: ${PRODUCTION_TENANT}${NC}"

CURRENT_TENANT=$(az account show --query "tenantId" -o tsv 2>/dev/null)
if [[ "$CURRENT_TENANT" != "$PRODUCTION_TENANT" ]]; then
    echo -e "${YELLOW}Switching from tenant ${CURRENT_TENANT} to ${PRODUCTION_TENANT}${NC}"
    az login --tenant "$PRODUCTION_TENANT" --only-show-errors > /dev/null
fi

echo -e "${BLUE}Generating production Azure AI token...${NC}"
PRODUCTION_TOKEN=$(az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv 2>/dev/null)
if [[ -z "$PRODUCTION_TOKEN" || ${#PRODUCTION_TOKEN} -lt 100 ]]; then
    echo -e "${RED}Failed to generate production token or token is invalid${NC}"
    exit 1
fi
echo -e "${GREEN}Production token generated successfully (length: ${#PRODUCTION_TOKEN})${NC}"

if [[ -n "$SOURCE_TENANT" && "$SOURCE_TENANT" != "$PRODUCTION_TENANT" ]]; then
    az login --tenant "$SOURCE_TENANT" --only-show-errors > /dev/null || true
elif [[ "$CURRENT_TENANT" != "$PRODUCTION_TENANT" ]]; then
    az login --tenant "$CURRENT_TENANT" --only-show-errors > /dev/null || true
fi

if ! docker image inspect v1-to-v2-migration > /dev/null 2>&1; then
    echo -e "${YELLOW}Docker image 'v1-to-v2-migration' not found. Building...${NC}"
    docker build -t v1-to-v2-migration .
fi

if [[ -f .env ]]; then
    echo -e "${GREEN}Loading environment variables from .env file${NC}"
    export $(grep -v '^#' .env | grep -v '^$' | xargs)
else
    echo -e "${YELLOW}No .env file found. Using process environment values.${NC}"
fi

echo -e "${GREEN}Running migration in Docker container with token authentication...${NC}"

DOCKER_CMD=(
    docker run --rm -it
    --network host
    -e DOCKER_CONTAINER=true
    -e LOCAL_HOST=host.docker.internal:5001
    -e AGENTS_HOST="${AGENTS_HOST:-}"
    -e AGENTS_SUBSCRIPTION="${AGENTS_SUBSCRIPTION:-}"
    -e AGENTS_RESOURCE_GROUP="${AGENTS_RESOURCE_GROUP:-}"
    -e AGENTS_WORKSPACE="${AGENTS_WORKSPACE:-}"
    -e AGENTS_API_VERSION="${AGENTS_API_VERSION:-}"
    -e COSMOS_CONNECTION_STRING="${COSMOS_CONNECTION_STRING:-}"
    -e COSMOS_DB_CONNECTION_STRING="${COSMOS_DB_CONNECTION_STRING:-}"
    -e COSMOS_DB_DATABASE_NAME="${COSMOS_DB_DATABASE_NAME:-}"
    -e COSMOS_DB_CONTAINER_NAME="${COSMOS_DB_CONTAINER_NAME:-}"
    -e PRODUCTION_PROJECT_ENDPOINT="${PRODUCTION_PROJECT_ENDPOINT:-}"
    -e PRODUCTION_PROJECT_NAME="${PRODUCTION_PROJECT_NAME:-}"
    -e AZURE_TENANT_ID="${AZURE_TENANT_ID:-}"
    -e AZURE_CLIENT_ID="${AZURE_CLIENT_ID:-}"
    -e AZURE_CLIENT_SECRET="${AZURE_CLIENT_SECRET:-}"
    -e AZURE_SUBSCRIPTION_ID="${AZURE_SUBSCRIPTION_ID:-}"
    -e AZURE_RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-}"
    -e AZURE_PROJECT_NAME="${AZURE_PROJECT_NAME:-}"
    -e NEED_BETA_VERSION="${NEED_BETA_VERSION}"
    -e AZ_TOKEN="${SOURCE_TOKEN}"
    -e PRODUCTION_TOKEN="${PRODUCTION_TOKEN}"
    -v "$HOME/.azure:/home/migration/.azure"
    v1-to-v2-migration
)

if [[ "$NEED_BETA_VERSION" == "true" ]]; then
    echo -e "${BLUE}Using prerelease azure-ai-projects build for connection string support${NC}"
else
    echo -e "${GREEN}Using stable azure-ai-projects 2.x${NC}"
fi

DOCKER_CMD+=("${POSITIONAL_ARGS[@]}")
"${DOCKER_CMD[@]}"
EXIT_CODE=$?

if [[ $EXIT_CODE -eq 0 ]]; then
    echo -e "${GREEN}Migration completed successfully.${NC}"
else
    echo -e "${RED}Migration failed with exit code: $EXIT_CODE${NC}"
fi

exit $EXIT_CODE