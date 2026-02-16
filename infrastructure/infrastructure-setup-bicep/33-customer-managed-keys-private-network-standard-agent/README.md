---
description: This template deploys an Azure AI Foundry account, project, and model deployment while using your key for encryption (Customer Managed Key) in full private setup.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: aifoundry-cmk
languages:
- bicep
- json
---
# Set up Azure AI Foundry using Customer Managed Keys for encryption

This Azure AI Foundry template demonstrates how to deploy AI Foundry with Agents private network standard setup and customer-managed keys for encryption.

## Prerequisites

* An existing Azure Key Vault resource. This sample template does not create it.
* You must enable both the Soft Delete and Do Not Purge properties on the existing Azure Key Vault instance.
* If you use the Key Vault firewall, you must allow trusted Microsoft services to access the Azure Key Vault.
* The template uses RBAC roles for keyvault and assign the identity of the AI Foundry account and global cosmos DB account "Key Vault Crypto Service Encryption User" permission on keyvault
* Only RSA and RSA-HSM keys of size 2048 are supported. For more information about keys, see Key Vault keys in 

## Features
This template provides same features in template `15-private-network-standard-agent-setup` for selecting existing resources, different subscription dns zones and all other features and it combines it with the encryption configuration from template `31-customer-managed-keys-standard-agent` for the standard setup but adding over the private network setup.

The current templates provides the following:
- `30-customer-managed-keys` : provides a sample on creating CMK foundry over basic setup and system-assigned managed identity
- `31-customer-managed-keys-standard-agent`: provides a sample on creating CMK foundry over public standard setup (where AI foundry and its dependent resource all have public network access enabled) and system-assigned managed identity
- `32-customer-managed-keys-user-assigned-identity`: provides a sample on creating CMK foundry over basic setup and user-assigned managed identity
- `33-customer-managed-keys-private-network-standard-agent`: provides a sample on creating CMK foundry over private network standard setup (where AI foundry has network injection on the same VNET as the dependent resources are connected to) and system-assigned managed identity

## Run the Bicep deployment commands

Steps:
   ```bash
   az deployment group create --resource-group <your-resource-group> --template-file main.bicep --parameters main.bicepparam
   ```


## Learn more
If you are new to Azure AI Foundry, see:

- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-foundry/)

If you are new to template deployment, see:

- [Azure Resource Manager documentation](https://learn.microsoft.com/azure/azure-resource-manager/)
- [Azure AI services quickstart article](https://learn.microsoft.com/azure/cognitive-services/resource-manager-template)

`Tags: Microsoft.CognitiveServices/accounts/projects`
