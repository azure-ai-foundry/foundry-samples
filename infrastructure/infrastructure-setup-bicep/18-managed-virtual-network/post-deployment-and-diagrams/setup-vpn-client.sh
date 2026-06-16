#!/bin/bash
# setup-vpn-client.sh - Configure P2S VPN for developer access to the private VNet
#
# This script:
#   1. Downloads VPN client configuration from Azure
#   2. Displays connection instructions for Azure VPN Client
#   3. Verifies VPN gateway is provisioned and P2S is configured
#
# Usage: ./setup-vpn-client.sh <resource-group>
#
# Prerequisites:
#   - VPN Gateway must be fully provisioned (takes ~30 min during deployment)
#   - Azure VPN Client installed on developer machine
#   - User must be in the Entra ID tenant configured for the VPN
set -e

RESOURCE_GROUP="${1:?Usage: ./setup-vpn-client.sh <resource-group>}"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

echo "=== P2S VPN Client Setup ==="
echo "Resource Group: $RESOURCE_GROUP"
echo ""

# ── Step 1: Find VPN Gateway ────────────────────────────────────
echo "[1/3] Discovering VPN Gateway..."
GW_NAME=$(az network vnet-gateway list -g "$RESOURCE_GROUP" --query "[0].name" -o tsv)
if [ -z "$GW_NAME" ]; then
  echo "ERROR: No VPN Gateway found in resource group '$RESOURCE_GROUP'"
  echo "  VPN Gateway takes ~30 minutes to deploy. Check deployment status."
  exit 1
fi

GW_STATUS=$(az network vnet-gateway show -g "$RESOURCE_GROUP" -n "$GW_NAME" --query "provisioningState" -o tsv)
echo "  Gateway: $GW_NAME (Status: $GW_STATUS)"

if [ "$GW_STATUS" != "Succeeded" ]; then
  echo "ERROR: VPN Gateway is not yet provisioned (status: $GW_STATUS)"
  echo "  Wait for deployment to complete before configuring VPN client."
  exit 1
fi

# Get P2S config details
TENANT_ID=$(az network vnet-gateway show -g "$RESOURCE_GROUP" -n "$GW_NAME" \
  --query "vpnClientConfiguration.aadTenant" -o tsv | sed 's|https://login.microsoftonline.com/||' | tr -d '/')
CLIENT_POOL=$(az network vnet-gateway show -g "$RESOURCE_GROUP" -n "$GW_NAME" \
  --query "vpnClientConfiguration.vpnClientAddressPool.addressPrefixes[0]" -o tsv)
echo "  Tenant: $TENANT_ID"
echo "  Client address pool: $CLIENT_POOL"
echo ""

# ── Step 2: Generate VPN client config ──────────────────────────
echo "[2/3] Generating VPN client configuration..."
CONFIG_URL=$(az network vnet-gateway vpn-client generate \
  -g "$RESOURCE_GROUP" \
  -n "$GW_NAME" \
  --authentication-method EapTls \
  -o tsv 2>/dev/null || \
az network vnet-gateway vpn-client generate \
  -g "$RESOURCE_GROUP" \
  -n "$GW_NAME" \
  -o tsv)

echo "  Config download URL: $CONFIG_URL"
echo ""

# Download if curl is available
if command -v curl &>/dev/null; then
  OUTPUT_FILE="vpn-client-config.zip"
  curl -sL "$CONFIG_URL" -o "$OUTPUT_FILE"
  echo "  Downloaded: $OUTPUT_FILE"
  echo ""
fi

# ── Step 3: Display instructions ────────────────────────────────
echo "[3/3] Connection Instructions"
echo ""
echo "========================================="
echo "  Azure VPN Client Setup (Entra ID Auth)"
echo "========================================="
echo ""
echo "1. Install Azure VPN Client:"
echo "   - Windows: Microsoft Store → 'Azure VPN Client'"
echo "   - macOS: App Store → 'Azure VPN Client'"
echo ""
echo "2. Import the VPN profile:"
echo "   - Extract $OUTPUT_FILE"
echo "   - Open Azure VPN Client → Import → Select 'azurevpnconfig.xml'"
echo "   - Or for OpenVPN: use the .ovpn file from AzureVPN/ folder"
echo ""
echo "3. Connect:"
echo "   - Click Connect → Authenticate with your Entra ID credentials"
echo "   - You'll be assigned an IP from: $CLIENT_POOL"
echo ""
echo "4. Verify connectivity:"
echo "   - Ping the jumpbox private IP (192.168.3.x)"
echo "   - Or SSH directly: ssh azureuser@<jumpbox-private-ip>"
echo ""
echo "Network routes available after VPN connect:"
echo "   192.168.0.0/16 → Customer VNet (all subnets)"
echo "   Including:"
echo "     192.168.1.0/24 - PE subnet (private endpoints)"
echo "     192.168.3.0/24 - Jumpbox subnet"
echo "     192.168.4.0/23 - Tools subnet (Container Apps)"
echo ""
echo "========================================="
echo ""
echo "NOTE: With Entra ID (managed identity) auth, you do NOT need"
echo "      client certificates. Just sign in with your Azure AD account."
