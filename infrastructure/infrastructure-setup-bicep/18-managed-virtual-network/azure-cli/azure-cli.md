# Managed Network and Outbound Rule CLI Commands

This document summarizes the new Azure CLI commands for configuring managed networks and outbound rules on Azure Cognitive Services accounts used with Microsoft Foundry.

These commands help you configure network isolation and control outbound traffic from a managed network.

## Command Groups

```azurecli
az cognitiveservices account managed-network
az cognitiveservices account managed-network outbound-rule
```

## Managed Network Commands

Use these commands to create, update, show, and provision the managed network for an Azure Cognitive Services account. You can run these commands after the Foundry resource was created. 

**Note:** Ensure you have assigned the Foundry account's managed identity the Azure AI Enterprise Network Connection Approver. This ensures the private endpoint to the Foundry resource from the managed VNET gets created. Command would be like this:

`az role assignment create --assignee-object-id <managed-identity-principal-id> --assignee-principal-type ServicePrincipal --role "Azure AI Enterprise Network Connection Approver" --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>`

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

## End-to-End Deployment: CLI Walkthrough

This section walks through the full sequence of commands to deploy a Foundry resource with a managed virtual network using the CLI. Each step must be completed in order.

> **Important:** The `az cognitiveservices account managed-network` CLI commands require a CLI extension that may not yet be available in your Azure CLI version. If the commands are not recognized, use the equivalent `az rest` commands shown below.

### Prerequisites

- Azure CLI installed and authenticated (`az login`)
- A resource group in a [supported region](#supported-regions)

### Step 1: Create the AI Services account with network injections

The account must be created with `customSubDomainName`, `allowProjectManagement`, and `networkInjections` set **at creation time**. These properties cannot be added after the account is created. 

> **Important:** you must use `az rest` commands for account creation with network injections as the azure CLI does not yet support creating a Foundry resource with network injection 

```azurecli
az rest --method PUT \
  --url "https://management.azure.com/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account-name>?api-version=2025-10-01-preview" \
  --body '{
    "location": "<region>",
    "kind": "AIServices",
    "sku": { "name": "S0" },
    "identity": { "type": "SystemAssigned" },
    "properties": {
      "allowProjectManagement": true,
      "customSubDomainName": "<account-name>",
      "networkInjections": [
        {
          "scenario": "agent",
          "subnetArmId": "",
          "useMicrosoftManagedNetwork": true
        }
      ],
      "disableLocalAuth": false
    }
  }' \
  --headers "Content-Type=application/json"
```

Wait for `provisioningState` to reach `Succeeded` before proceeding:

```azurecli
az rest --method GET \
  --url "https://management.azure.com/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account-name>?api-version=2025-10-01-preview" \
  --query "properties.provisioningState" -o tsv
```

### Step 2: Get the managed identity principal ID

Retrieve the system-assigned managed identity principal ID from the account:

```azurecli
az cognitiveservices account show \
  --resource-group <resource-group> \
  --name <account-name> \
  --query identity.principalId -o tsv
```

### Step 3: Assign the Network Connection Approver role

Assign the **Azure AI Enterprise Network Connection Approver** role to the account's managed identity. This allows managed network private endpoints to be auto-approved.

```azurecli
az role assignment create \
  --assignee-object-id <principal-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Azure AI Enterprise Network Connection Approver" \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>
```

> **Note:** If your target resources (Storage, Cosmos DB, AI Search) are in a different resource group, scope the role assignment to that resource group or to the subscription.

### Step 4: Create the managed network

Create the managed network child resource on the account. This establishes the network isolation mode and provisions the network infrastructure.

Using the CLI (when available):

```azurecli
az cognitiveservices account managed-network create \
  --resource-group <resource-group> \
  --name <account-name> \
  --managed-network allow_only_approved_outbound \
  --firewall-sku Standard
```

### Step 5: Add outbound rules

After the managed network is active, add outbound rules for approved destinations. See the [Outbound Rule Commands](#outbound-rule-commands) section above for the full set of CLI commands.

**FQDN rule example:**

```azurecli
az cognitiveservices account managed-network outbound-rule set \
  --resource-group <resource-group> \
  --name <account-name> \
  --rule my-fqdn-rule \
  --type fqdn \
  --destination "google.com"
```

**Service tag rule example:**

```azurecli
az cognitiveservices account managed-network outbound-rule set \
  --resource-group <resource-group> \
  --name <account-name> \
  --rule my-servicetag-rule \
  --type servicetag \
  --destination '{"serviceTag":"Storage","protocol":"TCP","portRanges":"443"}'
```

**Private endpoint rule example** (see [outbound-rules-az-rest.md](outbound-rules-az-rest.md) for `az rest` examples):

```azurecli
az cognitiveservices account managed-network outbound-rule set \
  --resource-group <resource-group> \
  --name <account-name> \
  --rule my-pe-rule \
  --type privateendpoint \
  --destination '{"serviceResourceId":"/subscriptions/<target-sub>/resourceGroups/<target-rg>/providers/Microsoft.Storage/storageAccounts/<storage-name>","subresourceTarget":"blob"}'
```

### Step 6: Verify the deployment

Confirm the managed network is active:

```azurecli
az cognitiveservices account managed-network show \
  --resource-group <resource-group> \
  --name <account-name>
```

List all outbound rules and their status:

```azurecli
az cognitiveservices account managed-network outbound-rule list \
  --resource-group <resource-group> \
  --name <account-name>
```

Show a specific outbound rule:

```azurecli
az cognitiveservices account managed-network outbound-rule show \
  --resource-group <resource-group> \
  --name <account-name> \
  --rule my-fqdn-rule
```

### Supported regions

The managed virtual network feature is available in the following regions: East US, East US 2, Japan East, France Central, UAE North, Brazil South, Spain Central, Germany West Central, Italy North, South Central US, Australia East, Sweden Central, Canada East, South Africa North, West US, West US 3, South India, UK South.