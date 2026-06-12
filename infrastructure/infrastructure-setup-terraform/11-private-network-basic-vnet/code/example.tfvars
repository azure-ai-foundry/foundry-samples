location = "eastus"

# Optional
virtual_network_address_space          = "192.168.0.0/16"
agent_subnet_address_prefix            = "192.168.0.0/24"
private_endpoint_subnet_address_prefix = "192.168.1.0/24"

# Set to true to create an Azure Container Registry with a private endpoint
enable_container_registry = false

# Optional: Developer IP CIDR for ACR push access (only used if enable_container_registry = true)
# developer_ip_cidr = "203.0.113.0/26"
