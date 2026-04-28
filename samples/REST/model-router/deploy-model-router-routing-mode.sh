
# Deploys a model-router deployment with a selected routing mode.
# WSL example: bash /mnt/c/Work/repos/foundry-samples-pr/samples/REST/model-router/deploy-model-router-routing-mode.sh quality

SUBSCRIPTION_ID="<subscription-id>"
RESOURCE_GROUP="<resource-group>"
ACCOUNT_NAME="<azure-ai-foundry-account-name>"
DEPLOYMENT_NAME="<deployment-name>"
API_VERSION="2025-10-01-preview"
# Valid values: balanced (default) | cost | quality
ROUTING_MODE="balanced"
SKU_NAME="GlobalStandard"
SKU_CAPACITY="10"
MODEL_FORMAT="OpenAI"
MODEL_NAME="model-router"
MODEL_VERSION="2025-11-18"

if [ $# -gt 0 ]; then
  ROUTING_MODE="$1"
fi

if [ -z "${AZURE_AI_AUTH_TOKEN:-}" ]; then
  AZURE_AI_AUTH_TOKEN="$(az account get-access-token --resource https://management.azure.com --query accessToken -o tsv)"
fi

case "${ROUTING_MODE}" in
  balanced|cost|quality)
    ;;
  *)
    echo "Invalid ROUTING_MODE: ${ROUTING_MODE}. Valid values: balanced | cost | quality" >&2
    exit 1
    ;;
esac

# <deploy_model_router_routing_mode>
curl -X PUT "https://management.azure.com/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.CognitiveServices/accounts/${ACCOUNT_NAME}/deployments/${DEPLOYMENT_NAME}?api-version=${API_VERSION}" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AZURE_AI_AUTH_TOKEN" \
  -d @- <<EOF
{
  "sku": {
    "name": "${SKU_NAME}",
    "capacity": ${SKU_CAPACITY}
  },
  "properties": {
    "model": {
      "format": "${MODEL_FORMAT}",
      "name": "${MODEL_NAME}",
      "version": "${MODEL_VERSION}"
    },
    "routing": {
      "mode": "${ROUTING_MODE}"
    }
  }
}
EOF
# </deploy_model_router_routing_mode>