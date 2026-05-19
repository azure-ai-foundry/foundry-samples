#!/usr/bin/env bash
# Deploy the weather agent to Azure Container Apps and generate traffic.
#
# Required env vars:
#   AZURE_SUBSCRIPTION_ID              - Azure subscription to deploy into
#   RESOURCE_GROUP                     - existing RG
#   LOCATION                           - e.g. eastus2
#   ACA_ENV                            - existing Container Apps env name
#   ACR_NAME                           - existing Azure Container Registry
#   APPLICATIONINSIGHTS_CONNECTION_STRING
#   AZURE_OPENAI_ENDPOINT
#   AZURE_OPENAI_DEPLOYMENT
#   AZURE_OPENAI_API_KEY               - or rely on AAD via DefaultAzureCredential
#
# Optional:
#   AGENT_NAME              (default: weather-agent)
#   IMAGE_TAG               (default: current timestamp)
#   TRACE_INGEST_WAIT_SECS  (default: 90)

set -euo pipefail

AGENT_NAME="${AGENT_NAME:-weather-agent}"
IMAGE_TAG="${IMAGE_TAG:-$(date +%Y%m%d%H%M%S)}"
TRACE_INGEST_WAIT_SECS="${TRACE_INGEST_WAIT_SECS:-90}"
IMAGE="${ACR_NAME}.azurecr.io/${AGENT_NAME}:${IMAGE_TAG}"

: "${AZURE_SUBSCRIPTION_ID:?}"
: "${RESOURCE_GROUP:?}"; : "${LOCATION:?}"; : "${ACA_ENV:?}"; : "${ACR_NAME:?}"
: "${APPLICATIONINSIGHTS_CONNECTION_STRING:?}"
: "${AZURE_OPENAI_ENDPOINT:?}"; : "${AZURE_OPENAI_DEPLOYMENT:?}"

echo "==> Selecting Azure subscription"
az account set --subscription "$AZURE_SUBSCRIPTION_ID" --only-show-errors

echo "==> Building and pushing image to ACR"
az acr build \
  --registry "$ACR_NAME" \
  --image "${AGENT_NAME}:${IMAGE_TAG}" \
  --no-logs \
  --only-show-errors \
  .

echo "==> Deploying to Azure Container Apps"
az containerapp create \
  --name "$AGENT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --environment "$ACA_ENV" \
  --image "$IMAGE" \
  --target-port 8000 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 2 \
  --registry-server "${ACR_NAME}.azurecr.io" \
  --system-assigned \
  --registry-identity system \
  --env-vars \
    AGENT_NAME="$AGENT_NAME" \
    OTEL_AGENT_ID="${OTEL_AGENT_ID:-$AGENT_NAME}" \
    APPLICATIONINSIGHTS_CONNECTION_STRING="$APPLICATIONINSIGHTS_CONNECTION_STRING" \
    AZURE_OPENAI_ENDPOINT="$AZURE_OPENAI_ENDPOINT" \
    AZURE_OPENAI_DEPLOYMENT="$AZURE_OPENAI_DEPLOYMENT" \
    AZURE_OPENAI_API_VERSION="${AZURE_OPENAI_API_VERSION:-2024-10-21}" \
    AZURE_OPENAI_API_KEY="${AZURE_OPENAI_API_KEY:-}" \
    --output none \
    --only-show-errors \
  || az containerapp update \
       --name "$AGENT_NAME" \
       --resource-group "$RESOURCE_GROUP" \
       --image "$IMAGE" \
    --set-env-vars \
      AGENT_NAME="$AGENT_NAME" \
      OTEL_AGENT_ID="${OTEL_AGENT_ID:-$AGENT_NAME}" \
      APPLICATIONINSIGHTS_CONNECTION_STRING="$APPLICATIONINSIGHTS_CONNECTION_STRING" \
      AZURE_OPENAI_ENDPOINT="$AZURE_OPENAI_ENDPOINT" \
      AZURE_OPENAI_DEPLOYMENT="$AZURE_OPENAI_DEPLOYMENT" \
      AZURE_OPENAI_API_VERSION="${AZURE_OPENAI_API_VERSION:-2024-10-21}" \
      AZURE_OPENAI_API_KEY="${AZURE_OPENAI_API_KEY:-}" \
      --output none \
      --only-show-errors

FQDN=$(az containerapp show -n "$AGENT_NAME" -g "$RESOURCE_GROUP" \
       --query properties.configuration.ingress.fqdn -o tsv --only-show-errors)
AGENT_URL="https://${FQDN}"
echo "==> Agent URL: $AGENT_URL"

echo "==> Waiting for /healthz"
for i in {1..30}; do
  if curl -fsS --max-time 10 "${AGENT_URL}/healthz" >/dev/null; then break; fi
  sleep 5
done

echo "==> Generating traffic"
AGENT_URL="$AGENT_URL" AGENT_NAME="$AGENT_NAME" OTEL_AGENT_ID="${OTEL_AGENT_ID:-$AGENT_NAME}" python generate_traffic.py

if [ "$TRACE_INGEST_WAIT_SECS" -gt 0 ]; then
  echo "==> Waiting ${TRACE_INGEST_WAIT_SECS}s for OTel export and ingestion"
  sleep "$TRACE_INGEST_WAIT_SECS"
fi

echo "==> Done. Agent URL: $AGENT_URL"
echo "==> Then run: python register_external_agent.py && python run_trace_eval.py"
