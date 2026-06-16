#!/bin/bash
# setup-jumpbox-access.sh - Configure jumpbox for SSH access and assign MI roles
#
# This script:
#   1. Assigns a public IP to the jumpbox (or uses existing)
#   2. Creates/updates NSG rules for SSH JIT access from your current IP
#   3. Assigns required RBAC roles to the jumpbox managed identity
#   4. Installs Azure CLI, pip, and jq on the jumpbox
#
# Usage: ./setup-jumpbox-access.sh <resource-group> [--install-tools]
#   --install-tools   (optional) SSH into the jumpbox and install Azure CLI, pip, and jq.
set -e

RESOURCE_GROUP="${1:?Usage: ./setup-jumpbox-access.sh <resource-group>}"
INSTALL_TOOLS="${2:-}"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

echo "=== Jumpbox Access Setup ==="
echo "Resource Group: $RESOURCE_GROUP"
echo ""

# ── Step 1: Find jumpbox resources ──────────────────────────────
echo "[1/5] Discovering jumpbox resources..."
VM_NAME=$(az vm list -g "$RESOURCE_GROUP" --query "[?contains(name,'jumpbox')].name | [0]" -o tsv)
if [ -z "$VM_NAME" ]; then
  echo "ERROR: No jumpbox VM found in resource group '$RESOURCE_GROUP'"
  exit 1
fi
NIC_NAME=$(az vm show -g "$RESOURCE_GROUP" -n "$VM_NAME" --query "networkProfile.networkInterfaces[0].id" -o tsv | xargs basename)
SUBNET_ID=$(az network nic show -g "$RESOURCE_GROUP" -n "$NIC_NAME" --query "ipConfigurations[0].subnet.id" -o tsv)
echo "  VM: $VM_NAME"
echo "  NIC: $NIC_NAME"
echo ""

# ── Step 2: Attach public IP ────────────────────────────────────
echo "[2/5] Configuring public IP..."
PIP_NAME="jumpbox-pip"
EXISTING_PIP=$(az network nic show -g "$RESOURCE_GROUP" -n "$NIC_NAME" --query "ipConfigurations[0].publicIPAddress.id" -o tsv 2>/dev/null)

if [ -z "$EXISTING_PIP" ] || [ "$EXISTING_PIP" = "null" ]; then
  echo "  Creating public IP '$PIP_NAME'..."
  az network public-ip create \
    -g "$RESOURCE_GROUP" \
    -n "$PIP_NAME" \
    --sku Standard \
    --allocation-method Static \
    -o none
  echo "  Attaching to NIC..."
  az network nic ip-config update \
    -g "$RESOURCE_GROUP" \
    --nic-name "$NIC_NAME" \
    --name ipconfig1 \
    --public-ip-address "$PIP_NAME" \
    -o none
fi

PUBLIC_IP=$(az network public-ip show -g "$RESOURCE_GROUP" -n "$PIP_NAME" --query ipAddress -o tsv 2>/dev/null || \
  az network nic show -g "$RESOURCE_GROUP" -n "$NIC_NAME" --query "ipConfigurations[0].publicIPAddress.id" -o tsv | xargs -I{} az network public-ip show --ids {} --query ipAddress -o tsv)
echo "  Public IP: $PUBLIC_IP"
echo ""

# ── Step 3: Configure NSG for SSH JIT ───────────────────────────
echo "[3/5] Configuring NSG for SSH access..."
MY_IP=$(curl -s https://api.ipify.org)/32
echo "  Your IP: $MY_IP"

# Find or create NSG
NSG_NAME=$(az network nsg list -g "$RESOURCE_GROUP" --query "[?contains(name,'jumpbox')].name | [0]" -o tsv)
if [ -z "$NSG_NAME" ]; then
  NSG_NAME="jumpboxNSG"
  echo "  Creating NSG '$NSG_NAME'..."
  LOCATION=$(az group show -n "$RESOURCE_GROUP" --query location -o tsv)
  az network nsg create -g "$RESOURCE_GROUP" -n "$NSG_NAME" -l "$LOCATION" -o none
  # Associate with NIC
  az network nic update -g "$RESOURCE_GROUP" -n "$NIC_NAME" --network-security-group "$NSG_NAME" -o none
fi

# Add SSH rule (JIT-like: only from your IP)
echo "  Adding SSH allow rule from $MY_IP..."
az network nsg rule create \
  -g "$RESOURCE_GROUP" \
  --nsg-name "$NSG_NAME" \
  -n "AllowSSH-JIT" \
  --priority 100 \
  --direction Inbound \
  --access Allow \
  --protocol Tcp \
  --source-address-prefixes "$MY_IP" \
  --destination-port-ranges 22 \
  -o none 2>/dev/null || \
az network nsg rule update \
  -g "$RESOURCE_GROUP" \
  --nsg-name "$NSG_NAME" \
  -n "AllowSSH-JIT" \
  --source-address-prefixes "$MY_IP" \
  -o none
echo "  NSG rule updated."
echo ""

# ── Step 4: Assign RBAC roles to jumpbox MI ─────────────────────
echo "[4/5] Assigning RBAC roles to jumpbox managed identity..."
VM_MI_PRINCIPAL=$(az vm show -g "$RESOURCE_GROUP" -n "$VM_NAME" --query "identity.principalId" -o tsv)

if [ -z "$VM_MI_PRINCIPAL" ] || [ "$VM_MI_PRINCIPAL" = "null" ]; then
  echo "  Enabling system-assigned managed identity..."
  az vm identity assign -g "$RESOURCE_GROUP" -n "$VM_NAME" -o none
  VM_MI_PRINCIPAL=$(az vm show -g "$RESOURCE_GROUP" -n "$VM_NAME" --query "identity.principalId" -o tsv)
fi

echo "  Jumpbox MI principal: $VM_MI_PRINCIPAL"

# Cognitive Services User (for calling Foundry API from jumpbox)
echo "  Assigning 'Cognitive Services User'..."
az role assignment create \
  --assignee "$VM_MI_PRINCIPAL" \
  --role "Cognitive Services User" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  -o none 2>/dev/null || echo "    (already assigned)"

# Azure AI Developer (for agent management)
echo "  Assigning 'Azure AI Developer'..."
az role assignment create \
  --assignee "$VM_MI_PRINCIPAL" \
  --role "Azure AI Developer" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  -o none 2>/dev/null || echo "    (already assigned)"

echo ""

# ── Step 5: Install tools on jumpbox ─────────────────────────────
if [ "$INSTALL_TOOLS" = "--install-tools" ]; then
  echo "[5/5] Installing Azure CLI, pip, and jq on jumpbox..."
  ssh -o StrictHostKeyChecking=no azureuser@"$PUBLIC_IP" << 'REMOTE_SCRIPT'
    set -e
    echo "Updating packages..."
    sudo apt-get update -qq
    echo "Installing jq, curl, pip..."
    sudo apt-get install -y -qq jq curl python3-pip
    echo "Installing Azure CLI..."
    curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
    echo "Done!"
    echo "  Azure CLI: $(az --version | head -1)"
    echo "  pip: $(pip3 --version)"
    echo "  jq: $(jq --version)"
REMOTE_SCRIPT
else
  echo "[5/5] Skipping tool installation (pass '--install-tools' as the second argument to enable)."
fi

echo ""
echo "=== Jumpbox Ready ==="
echo ""
echo "Connect: ssh azureuser@$PUBLIC_IP"
echo ""
echo "Test agent from jumpbox:"
echo "  TOKEN=\$(curl -s 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://cognitiveservices.azure.com' -H 'Metadata: true' | jq -r '.access_token')"
echo "  curl -X POST 'https://<account>.services.ai.azure.com/api/projects/<project>/agents/<agent-name>/endpoint/protocols/openai/responses?api-version=2025-11-15-preview' \\"
echo "    -H 'Authorization: Bearer \$TOKEN' -H 'Content-Type: application/json' \\"
echo "    -d '{\"input\":\"Hello\"}'"
