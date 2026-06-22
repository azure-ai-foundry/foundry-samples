# =============================================================================
# Azure Function on Private VNet — Example Configuration
#
# Deploy after the base TF 19 infrastructure is up.
# Fill in the resource_group_name and vnet_name from the base deployment outputs.
# =============================================================================

location            = "swedencentral"
resource_group_name = "rg-aifoundryXXXX"    # from: terraform output resource_group_name
vnet_name           = "vnet-aifoundryXXXX"  # from: terraform output vnet_id (extract name)
pe_subnet_name      = "pe-subnet"

# Integration subnet for Function App outbound VNet Integration
integration_subnet_name   = "func-integration-subnet"
integration_subnet_prefix = "192.168.5.0/24"
