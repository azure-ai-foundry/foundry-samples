# =============================================================================
# Azure AI Foundry — Private Network Agent Setup
# =============================================================================
# Copy this file to terraform.tfvars and fill in the required values.
#   cp example.tfvars terraform.tfvars
#
# For BYO (Bring Your Own) resources, uncomment and fill the relevant section.
# Any BYO variable left empty (or commented out) will create a new resource.
# =============================================================================

# ==============================
# REQUIRED — Target region
# ==============================
# See README.md for supported regions. Class B/C subnets (192.168.x.x, 172.16.x.x)
# are supported in all regions. Class A (10.x.x.x) is limited to specific regions.
location = "swedencentral"

# ==============================
# OPTIONAL — Resource configuration
# ==============================
ai_services_name_prefix = "aifoundry"
project_name            = "agent-project"
vnet_address_space      = ["192.168.0.0/16"]

# ==============================
# OPTIONAL — Model deployment
# ==============================
model_name     = "gpt-4.1"
model_version  = "2025-04-14"
model_capacity = 40

# ==============================
# BYO — Resource Group
# ==============================
# Provide an existing resource group name. All new resources deploy into this RG.
# Leave empty to create a new one.
existing_resource_group_name = ""

# ==============================
# BYO — Networking
# ==============================
# Provide an existing VNet and all three subnet IDs together.
# The agent and MCP subnets must be delegated to Microsoft.App/environments.
# The PE subnet must have no delegation.
# Leave empty to create new networking resources.
existing_vnet_id         = ""
existing_agent_subnet_id = ""
existing_pe_subnet_id    = ""
existing_mcp_subnet_id   = ""

# ==============================
# BYO — Backend Services
# ==============================
# Provide existing resource IDs. Each can be set independently.
# These resources must already have public access disabled and be in the same region.
# Leave empty to create new ones.
existing_storage_account_id  = ""
existing_cosmosdb_account_id = ""
existing_ai_search_id        = ""

# ==============================
# BYO — Private DNS Zones
# ==============================
# If your DNS zones are centrally managed (e.g., hub-spoke topology), provide the
# resource group (and optionally subscription) where all 6 zones exist:
#   privatelink.cognitiveservices.azure.com, privatelink.openai.azure.com,
#   privatelink.services.ai.azure.com, privatelink.blob.core.windows.net,
#   privatelink.search.windows.net, privatelink.documents.azure.com
# Leave empty to create new zones.
existing_dns_zones_resource_group  = ""
existing_dns_zones_subscription_id = ""

# ==============================
# BYO — Fabric (optional)
# ==============================
# Provide an existing Fabric workspace ID to create a Data Agent private endpoint.
# Leave empty to skip Fabric integration.
existing_fabric_workspace_id = ""

# ==============================
# OPTIONAL — Azure Container Registry
# ==============================
# Enable to create a Premium ACR with Private Endpoint for hosted agent containers.
# Set developer_ip_cidr to allow push access from your IP (e.g., your VPN or dev machine).
enable_container_registry = false
developer_ip_cidr         = ""
