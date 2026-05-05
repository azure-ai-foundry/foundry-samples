# Managed Network and Outbound Rule CLI Commands

This document summarizes the new Azure CLI commands for configuring managed networks and outbound rules on Azure Cognitive Services accounts used with Microsoft Foundry.

These commands help you configure network isolation and control outbound traffic from a managed network.

## Command Groups

```azurecli
az cognitiveservices account managed-network
az cognitiveservices account managed-network outbound-rule
```

## Managed Network Commands

Use these commands to create, update, show, and provision the managed network for an Azure Cognitive Services account.

| Command | Description |
| --- | --- |
| `az cognitiveservices account managed-network create` | Creates a managed network. Supports `allow_internet_outbound` or `allow_only_approved_outbound` isolation mode and an optional firewall SKU. |
| `az cognitiveservices account managed-network update` | Updates managed network settings for an existing account. |
| `az cognitiveservices account managed-network show` | Shows the current managed network settings for an account. |
| `az cognitiveservices account managed-network provision-network` | Provisions the managed network after it is configured. |

### Create a managed network with internet outbound access

Use `allow_internet_outbound` when the managed network should allow outbound internet access.

```azurecli
az cognitiveservices account managed-network create \
	--resource-group myResourceGroup \
	--name myAccount \
	--managed-network allow_internet_outbound
```

### Create a managed network with approved outbound only

Use `allow_only_approved_outbound` when outbound traffic should be restricted to approved outbound rules. The optional `--firewall-sku` parameter configures the managed network firewall SKU.

```azurecli
az cognitiveservices account managed-network create \
	--resource-group myResourceGroup \
	--name myAccount \
	--managed-network allow_only_approved_outbound \
	--firewall-sku Standard
```

### Show managed network settings

Use `show` to verify the current managed network configuration for an account.

```azurecli
az cognitiveservices account managed-network show \
	--resource-group myResourceGroup \
	--name myAccount
```

### Provision the managed network

Use `provision-network` after configuring the managed network to apply and provision the network settings.

```azurecli
az cognitiveservices account managed-network provision-network \
	--resource-group myResourceGroup \
	--name myAccount
```

## Outbound Rule Commands

Use outbound rules to define approved destinations when the managed network uses approved outbound access.

| Command | Description |
| --- | --- |
| `az cognitiveservices account managed-network outbound-rule list` | Lists all outbound rules for the managed network. |
| `az cognitiveservices account managed-network outbound-rule show` | Shows details for a specific outbound rule. |
| `az cognitiveservices account managed-network outbound-rule remove` | Deletes an outbound rule from the managed network. |
| `az cognitiveservices account managed-network outbound-rule set` | Creates or updates one outbound rule. Supports FQDN, private endpoint, and service tag rules. |
| `az cognitiveservices account managed-network outbound-rule bulk-set` | Creates or updates multiple outbound rules from a YAML or JSON file. |

## Outbound Rule Types

The `set` command supports these outbound rule types:

| Type | Description | Example destination |
| --- | --- | --- |
| `fqdn` | Allows outbound traffic to a fully qualified domain name. | `"*.openai.azure.com"` |
| `privateendpoint` | Allows outbound traffic through a private endpoint rule. | Private endpoint configuration JSON |
| `servicetag` | Allows outbound traffic to an Azure service tag, protocol, and port range. | `'{"serviceTag":"Storage","protocol":"TCP","portRanges":"443"}'` |

### Create or update an FQDN outbound rule

Use an FQDN rule to allow traffic to a domain name or wildcard domain.

```azurecli
az cognitiveservices account managed-network outbound-rule set \
	--resource-group myResourceGroup \
	--name myAccount \
	--rule my-fqdn-rule \
	--type fqdn \
	--destination "*.openai.azure.com"
```

### Create or update a service tag outbound rule

Use a service tag rule to allow traffic to an Azure service tag over a specific protocol and port range.

```azurecli
az cognitiveservices account managed-network outbound-rule set \
	--resource-group myResourceGroup \
	--name myAccount \
	--rule my-servicetag-rule \
	--type servicetag \
	--destination '{"serviceTag":"Storage","protocol":"TCP","portRanges":"443"}'
```

### List outbound rules

Use `list` to view all configured outbound rules for the managed network.

```azurecli
az cognitiveservices account managed-network outbound-rule list \
	--resource-group myResourceGroup \
	--name myAccount
```

### Show an outbound rule

Use `show` to inspect one outbound rule by name.

```azurecli
az cognitiveservices account managed-network outbound-rule show \
	--resource-group myResourceGroup \
	--name myAccount \
	--rule my-fqdn-rule
```

### Bulk create or update outbound rules

Use `bulk-set` to create or update multiple outbound rules from a YAML or JSON file.

```azurecli
az cognitiveservices account managed-network outbound-rule bulk-set \
	--resource-group myResourceGroup \
	--name myAccount \
	--file rules.yaml
```

### Remove an outbound rule

Use `remove` to delete an outbound rule from the managed network.

```azurecli
az cognitiveservices account managed-network outbound-rule remove \
	--resource-group myResourceGroup \
	--name myAccount \
	--rule my-fqdn-rule
```