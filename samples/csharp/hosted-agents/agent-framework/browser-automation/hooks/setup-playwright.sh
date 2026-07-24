#!/bin/sh
set -e

# ── Setup Playwright Workspace Connection ─────────────────────────────────────
# This script sets up the Playwright connection via Bicep deployment.
# In interactive mode it prompts for inputs; in CI / --no-prompt mode it reads
# pre-set azd env vars instead.
#
# Supported env vars (set via `azd env set` for non-interactive use):
#   PLAYWRIGHT_SERVICE_RESOURCE_ID  — ARM resource ID of an existing workspace (omit to create new)
#   PLAYWRIGHT_REGION               — Region for a new workspace (e.g. "eastus"); ignored when using existing
#   PLAYWRIGHT_AUTH_TYPE             — ProjectManagedIdentity (default) or AgenticIdentityToken. ApiKey is interactive-only.

echo ""
echo "This agent requires a Playwright Workspace connection."
echo ""

# ── Read pre-set values from azd env ──────────────────────────────────────────
# Load all env values once and look up by key. Avoids per-key calls to
# azd env get-value which outputs ERROR to stdout on missing keys.

_AZD_ENV_CACHE=$(azd env get-values 2>/dev/null || true)

_azd_get() {
    printf '%s' "$_AZD_ENV_CACHE" | grep "^${1}=" | head -1 | sed 's/^[^=]*="//' | sed 's/"$//'
}

PLAYWRIGHT_RESOURCE_ID=$(_azd_get PLAYWRIGHT_SERVICE_RESOURCE_ID)
PLAYWRIGHT_REGION=$(_azd_get PLAYWRIGHT_REGION)
AUTH_TYPE=$(_azd_get PLAYWRIGHT_AUTH_TYPE)
SUBSCRIPTION_ID=$(_azd_get AZURE_SUBSCRIPTION_ID)

# ── Determine if we can prompt ─────────────────────────────────────────────────
# azd hooks with `interactive: true` always connect stdin, even with --no-prompt.
# For non-interactive use, pre-set the env vars via `azd env set` before running.

if [ -z "$PLAYWRIGHT_RESOURCE_ID" ] && [ -z "$PLAYWRIGHT_REGION" ]; then
    if [ -t 0 ]; then
        printf "Enter an existing Playwright workspace ARM resource ID\n"
        printf "  (leave empty to create a new one)\n"
        printf "  (e.g., /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.LoadTestService/playwrightWorkspaces/{name})\n"
        printf "> "
        read -r PLAYWRIGHT_RESOURCE_ID
    else
        echo "Non-interactive mode: no PLAYWRIGHT_SERVICE_RESOURCE_ID or PLAYWRIGHT_REGION set."
        echo "  Creating a new workspace in the default region (eastus)."
        PLAYWRIGHT_REGION="eastus"
    fi
fi

if [ -z "$PLAYWRIGHT_RESOURCE_ID" ]; then
    # Creating a new workspace — need a region
    if [ -z "$PLAYWRIGHT_REGION" ]; then
        echo ""
        echo "A new Playwright workspace will be created."
        REGION_LIST=$(az provider show --namespace Microsoft.LoadTestService \
            --subscription "$SUBSCRIPTION_ID" \
            --query "resourceTypes[?resourceType=='playwrightWorkspaces'].locations | [0]" -o tsv 2>/dev/null || echo "")

        if [ -z "$REGION_LIST" ]; then
            REGION_LIST="East US
East Asia
West Europe
West US 3"
        fi

        i=1
        echo "Select region for the new workspace:"
        echo "$REGION_LIST" | while IFS= read -r region; do
            echo "  $i) $region"
            i=$((i + 1))
        done

        TOTAL=$(echo "$REGION_LIST" | wc -l | tr -d ' ')
        printf "Select (1-$TOTAL) [default: 1]: "
        read -r REGION_CHOICE

        if ! echo "$REGION_CHOICE" | grep -qE '^[0-9]+$' || [ "$REGION_CHOICE" -lt 1 ] || [ "$REGION_CHOICE" -gt "$TOTAL" ] 2>/dev/null; then
            REGION_CHOICE=1
        fi

        REGION_DISPLAY=$(echo "$REGION_LIST" | sed -n "${REGION_CHOICE}p")
        PLAYWRIGHT_REGION=$(echo "$REGION_DISPLAY" | tr '[:upper:]' '[:lower:]' | tr -d ' ')
        echo "  Using region: $REGION_DISPLAY ($PLAYWRIGHT_REGION)"
    else
        echo "Using pre-configured region: $PLAYWRIGHT_REGION"
    fi
else
    echo "Using existing workspace: $PLAYWRIGHT_RESOURCE_ID"
fi

# Auth type selection
API_KEY=""

if [ -z "$AUTH_TYPE" ]; then
    if [ -t 0 ]; then
        echo ""
        echo "Select authentication type:"
        if [ -n "$PLAYWRIGHT_RESOURCE_ID" ]; then
            echo "  1) Project Managed Identity (recommended)"
            echo "  2) Agent Identity"
            echo "  3) API Key"
            printf "Select (1-3) [default: 1]: "
            read -r AUTH_CHOICE

            case $AUTH_CHOICE in
                2) AUTH_TYPE="AgenticIdentityToken" ;;
                3) AUTH_TYPE="ApiKey" ;;
                *) AUTH_TYPE="ProjectManagedIdentity" ;;
            esac
        else
            echo "  1) Project Managed Identity (recommended)"
            echo "  2) Agent Identity"
            printf "Select (1-2) [default: 1]: "
            read -r AUTH_CHOICE

            case $AUTH_CHOICE in
                2) AUTH_TYPE="AgenticIdentityToken" ;;
                *) AUTH_TYPE="ProjectManagedIdentity" ;;
            esac
        fi
    else
        AUTH_TYPE="ProjectManagedIdentity"
        echo "Non-interactive mode: defaulting to ProjectManagedIdentity auth."
    fi
else
    echo "Using pre-configured auth type: $AUTH_TYPE"
fi

# ApiKey auth requires an existing workspace — new workspaces set credentials: null
if [ -z "$PLAYWRIGHT_RESOURCE_ID" ] && [ "$AUTH_TYPE" = "ApiKey" ]; then
    echo "Error: API Key authentication requires an existing Playwright workspace." >&2
    echo "  Set PLAYWRIGHT_SERVICE_RESOURCE_ID or choose a managed identity auth type." >&2
    exit 1
fi

if [ "$AUTH_TYPE" = "ApiKey" ]; then
    printf "Enter the Playwright access token: "
    stty -echo
    read -r API_KEY
    stty echo
    echo ""
    if [ -z "$API_KEY" ]; then
        echo "Error: Access token is required when using API Key authentication." >&2
        exit 1
    fi
fi

# ── Deploy Bicep ──────────────────────────────────────────────────────────────

RESOURCE_GROUP=$(_azd_get AZURE_RESOURCE_GROUP)
AI_ACCOUNT_NAME=$(_azd_get AZURE_AI_ACCOUNT_NAME)
AI_PROJECT_NAME=$(_azd_get AZURE_AI_PROJECT_NAME)

if [ -z "$RESOURCE_GROUP" ] || [ -z "$AI_ACCOUNT_NAME" ] || [ -z "$AI_PROJECT_NAME" ]; then
    echo "Error: AZURE_RESOURCE_GROUP, AZURE_AI_ACCOUNT_NAME, and AZURE_AI_PROJECT_NAME must be set." >&2
    exit 1
fi

echo ""
echo "Deploying Playwright connection..."

SCRIPT_DIR=$(dirname "$0")
BICEP_FILE="$SCRIPT_DIR/../infra-modules/playwright-connection.bicep"

# Build parameters as a JSON file to avoid exposing secrets in process arguments
PARAMS_FILE=$(mktemp)
trap 'rm -f "$PARAMS_FILE"' EXIT INT TERM

{
  printf '{\n'
  printf '  "aiFoundryAccountName": { "value": "%s" },\n' "$AI_ACCOUNT_NAME"
  printf '  "aiFoundryProjectName": { "value": "%s" },\n' "$AI_PROJECT_NAME"
  printf '  "authType": { "value": "%s" }' "$AUTH_TYPE"
  [ -n "$PLAYWRIGHT_RESOURCE_ID" ] && printf ',\n  "playwrightResourceId": { "value": "%s" }' "$PLAYWRIGHT_RESOURCE_ID"
  [ -n "$PLAYWRIGHT_REGION" ] && printf ',\n  "playwrightRegion": { "value": "%s" }' "$PLAYWRIGHT_REGION"
  [ "$AUTH_TYPE" = "ApiKey" ] && printf ',\n  "apiKey": { "value": "%s" }' "$API_KEY"
  printf '\n}\n'
} > "$PARAMS_FILE"

RESULT=$(az deployment group create \
    --name "playwright-connection-$(date +%s)" \
    --resource-group "$RESOURCE_GROUP" \
    --subscription "$SUBSCRIPTION_ID" \
    --template-file "$BICEP_FILE" \
    --parameters "@$PARAMS_FILE" \
    --query "properties.outputs" -o json \
    --only-show-errors)

if [ $? -ne 0 ]; then
    echo "Error: Failed to deploy Playwright connection." >&2
    exit 1
fi

# ── Store outputs in azd env ──────────────────────────────────────────────────

RESOLVED_RESOURCE_ID=$(echo "$RESULT" | python3 -c "import sys,json; print(json.load(sys.stdin)['playwrightResourceId']['value'])")

azd env set PLAYWRIGHT_SERVICE_RESOURCE_ID "$RESOLVED_RESOURCE_ID"
azd env set PLAYWRIGHT_AUTH_TYPE "$AUTH_TYPE"
azd env set PLAYWRIGHT_CONNECTION_CONFIGURED "true"

echo ""
echo "✅ Playwright connection created successfully."
echo "   Auth: $AUTH_TYPE"
if [ -z "$PLAYWRIGHT_RESOURCE_ID" ]; then
    echo "   Resource ID: $RESOLVED_RESOURCE_ID"
    printf "   Portal Link: \033[36mhttps://portal.azure.com/#@/resource${RESOLVED_RESOURCE_ID}\033[0m\n"
fi
