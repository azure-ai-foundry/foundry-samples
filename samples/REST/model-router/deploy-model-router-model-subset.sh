# Deploys a model-router deployment that routes only to a custom subset of models.
# WSL example: bash /mnt/c/Work/repos/foundry-samples-pr/samples/REST/model-router/deploy-model-router-model-subset.sh

SUBSCRIPTION_ID="<subscription-id>"
RESOURCE_GROUP="<resource-group>"
ACCOUNT_NAME="<azure-ai-foundry-account-name>"
DEPLOYMENT_NAME="<deployment-name>"
API_VERSION="2025-10-01-preview"
SKU_NAME="GlobalStandard"
SKU_CAPACITY="10"
MODEL_ROUTER_FORMAT="OpenAI"
MODEL_ROUTER_NAME="model-router"
MODEL_ROUTER_VERSION="2025-11-18"
SUBSET_1_FORMAT="OpenAI"
SUBSET_1_NAME="gpt-4.1"
SUBSET_1_VERSION="2025-04-14"
SUBSET_2_FORMAT="OpenAI"
SUBSET_2_NAME="gpt-5.2-chat"
SUBSET_2_VERSION="2025-12-11"
SUBSET_3_FORMAT="Meta"
SUBSET_3_NAME="Llama-4-Maverick-17B-128E-Instruct-FP8"
SUBSET_3_VERSION="1"

if [ -z "${AZURE_AI_AUTH_TOKEN:-}" ]; then
  AZURE_AI_AUTH_TOKEN="$(az account get-access-token --resource https://management.azure.com --query accessToken -o tsv)"
fi

# <deploy_model_router_model_subset>
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
      "format": "${MODEL_ROUTER_FORMAT}",
      "name": "${MODEL_ROUTER_NAME}",
      "version": "${MODEL_ROUTER_VERSION}"
    },
    "routing": {
      "models": [
        {
          "format": "${SUBSET_1_FORMAT}",
          "name": "${SUBSET_1_NAME}",
          "version": "${SUBSET_1_VERSION}"
        },
        {
          "format": "${SUBSET_2_FORMAT}",
          "name": "${SUBSET_2_NAME}",
          "version": "${SUBSET_2_VERSION}"
        },
        {
          "format": "${SUBSET_3_FORMAT}",
          "name": "${SUBSET_3_NAME}",
          "version": "${SUBSET_3_VERSION}"
        }
      ]
    }
  }
}
EOF
# </deploy_model_router_model_subset>