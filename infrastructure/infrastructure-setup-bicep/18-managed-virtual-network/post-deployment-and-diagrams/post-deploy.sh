#!/bin/bash
# post-deploy.sh - Post-deployment: Add outbound PE rules + approve PE connections
# Run after main.bicep deployment completes
#
# This creates outbound PE rules from the Foundry managed VNet to:
#   1. Foundry Account itself (self-PE, CRITICAL for hosted agents)
#   2. Storage Account (blob access for file uploads)
#   3. CosmosDB (agent thread state)
#   4. AI Search (vector store / file search)
#   5. Container Apps Environment (MCP/A2A/OpenAPI tool servers)
#
# Prerequisites:
#   - AI Services MI must have Contributor + "Azure AI Enterprise Network Connection Approver"
#     at the resource group level for auto-approval of PE connections
#   - Managed network must already be provisioned on the AI Services account
#
# Why is the self-PE needed?
#   Hosted agent containers run in Microsoft's Managed VNet. When the container
#   calls the model (e.g., GPT-4o) it needs to reach the Foundry account via
#   private link. Without the self-PE, traffic goes public → blocked by
#   publicNetworkAccess: Disabled → 500(403).
#   Prompt agents don't need this because they execute inside the account itself.
set -e

RESOURCE_GROUP="${1:?Usage: ./post-deploy.sh <resource-group> <ai-services-name>}"
AI_SERVICES_NAME="${2:?Usage: ./post-deploy.sh <resource-group> <ai-services-name>}"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
API_VERSION="2025-10-01-preview"

echo "=== Post-Deployment: Configure Managed VNet Outbound PE Rules ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "AI Services: $AI_SERVICES_NAME"
echo "Subscription: $SUBSCRIPTION_ID"
echo ""

# Discover resource IDs
AI_SERVICES_ID="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.CognitiveServices/accounts/$AI_SERVICES_NAME"
STORAGE_ID=$(az storage account list -g "$RESOURCE_GROUP" --query "[0].id" -o tsv)
CAE_NAME=$(az containerapp env list -g "$RESOURCE_GROUP" --query "[0].name" -o tsv)
CAE_ID=$(az containerapp env show -n "$CAE_NAME" -g "$RESOURCE_GROUP" --query id -o tsv 2>/dev/null || echo "")
COSMOS_ID=$(az cosmosdb list -g "$RESOURCE_GROUP" --query "[0].id" -o tsv)
SEARCH_ID=$(az search service list -g "$RESOURCE_GROUP" --query "[0].id" -o tsv)

echo "Found resources:"
echo "  AI Services: $AI_SERVICES_ID"
echo "  Storage: $STORAGE_ID"
echo "  CosmosDB: $COSMOS_ID"
echo "  Search: $SEARCH_ID"
echo "  CAE: ${CAE_NAME:-none} (${CAE_ID:-not deployed})"
echo ""

# ── Helper function ──────────────────────────────────────────────
add_outbound_pe_rule() {
  local RULE_NAME=$1
  local SERVICE_ID=$2
  local SUB_RESOURCE=$3

  echo "Adding outbound PE rule '$RULE_NAME' (subresource: $SUB_RESOURCE)..."
  az rest --method PUT \
    --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.CognitiveServices/accounts/$AI_SERVICES_NAME/managedNetworks/default/outboundRules/${RULE_NAME}?api-version=$API_VERSION" \
    --body "{\"properties\":{\"type\":\"PrivateEndpoint\",\"category\":\"UserDefined\",\"destination\":{\"serviceResourceId\":\"$SERVICE_ID\",\"subresourceTarget\":\"$SUB_RESOURCE\"}}}" \
    --headers Content-Type=application/json 
  echo "  Rule '$RULE_NAME' submitted."
}

# ── Step 1: Create outbound PE rules ────────────────────────────
echo "[1/4] Creating outbound PE rules..."

# CRITICAL: Self-PE (Foundry account → itself via managed VNet)
# Without this, hosted agent containers cannot call the model privately
echo "  [CRITICAL] Adding self-PE (foundry-account-pe) for hosted agent scenarios..."
add_outbound_pe_rule "foundry-account-pe" "$AI_SERVICES_ID" "account"

# Storage PE (for file uploads, code interpreter)
add_outbound_pe_rule "storage-pe" "$STORAGE_ID" "blob"

# CosmosDB PE (agent thread state persistence)
add_outbound_pe_rule "cosmosdb-pe" "$COSMOS_ID" "Sql"

# Search PE (vector store, file search capability)
add_outbound_pe_rule "search-pe" "$SEARCH_ID" "searchService"

# CAE PE (tools access - only if CAE is deployed)
if [ -n "$CAE_ID" ]; then
  add_outbound_pe_rule "tools-cae-pe" "$CAE_ID" "managedEnvironments"
fi
echo ""

# ── Step 2: Wait for provisioning ───────────────────────────────
echo "[2/4] Waiting for rules to provision (auto-approval via MI)..."
echo "  This typically takes 1-3 minutes..."
sleep 30

RULES_TO_CHECK="foundry-account-pe storage-pe cosmosdb-pe search-pe"
if [ -n "$CAE_ID" ]; then
  RULES_TO_CHECK="$RULES_TO_CHECK tools-cae-pe"
fi

for RULE_NAME in $RULES_TO_CHECK; do
  echo -n "  $RULE_NAME: "
  for i in $(seq 1 20); do
    STATUS=$(az rest --method GET \
      --uri "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.CognitiveServices/accounts/$AI_SERVICES_NAME/managedNetworks/default/outboundRules/${RULE_NAME}?api-version=$API_VERSION" \
      --query "properties.status" -o tsv 2>/dev/null)
    if [ "$STATUS" = "Active" ]; then
      echo "Active ✓"
      break
    elif [ "$STATUS" = "Failed" ]; then
      echo "FAILED ✗ (check RBAC: MI needs Contributor at RG scope)"
      break
    fi
    sleep 10
  done
  if [ "$STATUS" = "Provisioning" ]; then
    echo "still provisioning after 200s - check manually"
  fi
done
echo ""

# ── Step 3: Verify PE connections on target resources ───────────
echo "[3/4] Checking PE connection approval status..."

# Self-PE (on AI Services account)
SELF_PE_STATUS=$(az rest --method GET \
  --uri "https://management.azure.com${AI_SERVICES_ID}?api-version=2025-06-01" \
  --query "properties.privateEndpointConnections[?contains(name,'foundry-account-pe')].properties.privateLinkServiceConnectionState.status | [0]" -o tsv 2>/dev/null)
echo "  Self-PE (foundry-account-pe): ${SELF_PE_STATUS:-not found}"

# Storage PE
STORAGE_PE_STATUS=$(az rest --method GET \
  --uri "https://management.azure.com${STORAGE_ID}/privateEndpointConnections?api-version=2023-05-01" \
  --query "value[?contains(name,'storage-pe')].properties.privateLinkServiceConnectionState.status | [0]" -o tsv 2>/dev/null)
echo "  Storage PE: ${STORAGE_PE_STATUS:-not found}"

# CAE PE
if [ -n "$CAE_ID" ]; then
  CAE_PE_STATUS=$(az rest --method GET \
    --uri "https://management.azure.com${CAE_ID}/privateEndpointConnections?api-version=2024-10-02-preview" \
    --query "value[?contains(name,'tools-cae-pe')].properties.privateLinkServiceConnectionState.status | [0]" -o tsv 2>/dev/null)
  echo "  CAE PE: ${CAE_PE_STATUS:-not found}"
fi

# Search PE
SEARCH_PE_STATUS=$(az rest --method GET \
  --uri "https://management.azure.com${SEARCH_ID}/privateEndpointConnections?api-version=2025-05-01" \
  --query "value[?contains(name,'search-pe')].properties.privateLinkServiceConnectionState.status | [0]" -o tsv 2>/dev/null)
echo "  Search PE: ${SEARCH_PE_STATUS:-not found}"

# CosmosDB PE
COSMOS_PE_STATUS=$(az rest --method GET \
  --uri "https://management.azure.com${COSMOS_ID}/privateEndpointConnections?api-version=2024-05-15" \
  --query "value[?contains(name,'cosmosdb-pe')].properties.privateLinkServiceConnectionState.status | [0]" -o tsv 2>/dev/null)
echo "  CosmosDB PE: ${COSMOS_PE_STATUS:-not found}"

echo ""

# ── Step 4: Validate hosted agent connectivity ──────────────────
echo "[4/4] Verifying managed VNet configuration..."
OUTBOUND_RULES=$(az rest --method GET \
  --uri "https://management.azure.com${AI_SERVICES_ID}/managedNetworks/default?api-version=$API_VERSION" \
  --query "properties.outboundRules | keys(@)" -o tsv 2>/dev/null)
echo "  Active outbound rules:"
echo "$OUTBOUND_RULES" | while read -r rule; do
  echo "    - $rule"
done

PE_COUNT=$(az rest --method GET \
  --uri "https://management.azure.com${AI_SERVICES_ID}?api-version=2025-06-01" \
  --query "properties.privateEndpointConnections | length(@)" -o tsv 2>/dev/null)
echo "  Total PE connections on account: $PE_COUNT (expect ≥2: customer VNet PE + self-PE)"

echo ""
echo "=== Done! ==="
echo ""
echo "If any PE shows 'Pending', the AI Services MI may not have Contributor role at RG scope."
echo "Fix: az role assignment create --assignee <AI-Services-MI-Principal-ID> --role Contributor --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"
echo ""
echo "IMPORTANT: If hosted agents return 500(403), verify 'foundry-account-pe' is Active."
echo "  Check: az rest --method GET --uri 'https://management.azure.com${AI_SERVICES_ID}/managedNetworks/default/outboundRules/foundry-account-pe?api-version=$API_VERSION' --query 'properties.status' -o tsv"
echo ""
if [ -n "$CAE_ID" ]; then
  echo "Tool FQDNs (use in agent toolset):"
  az containerapp list -g "$RESOURCE_GROUP" --query "[].{name:name, fqdn:properties.configuration.ingress.fqdn}" -o table
fi
