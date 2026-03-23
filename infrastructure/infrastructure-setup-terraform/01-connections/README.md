# Connections Terraform Examples

Connections enable your AI applications to access tools and objects managed elsewhere in or outside of Azure.

This folder provides Terraform examples for the most common connection categories:

- **AI Search**: Connect to Azure AI Search (Cognitive Search) services
- **Key Vault**: Securely access secrets and keys
- **Application Insights**: Enable monitoring and telemetry
- **Azure OpenAI**: Connect to Azure OpenAI endpoints
- **Storage Account**: Access Azure Storage for data
- **Cosmos DB**: Connect to Cosmos DB databases
- **Bing Grounding**: Enable Bing Search for grounding

## Usage

Each file demonstrates how to create a specific connection type using Terraform with the AzAPI provider. These are example snippets that you can integrate into your own Terraform configurations.

## Prerequisites

- An existing Microsoft Foundry account
- The target resource you want to connect (e.g., AI Search service, Key Vault, etc.)
- Appropriate permissions to create connections

## Important Notes

- Uses AzAPI provider for connection resources (not yet in Az
ureRM)
- Some connections require API keys or managed identity configuration
- Role assignments may be needed for managed identity auth

## Documentation

- [Microsoft Foundry connections](https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/connections)
- [AzAPI Provider](https://registry.terraform.io/providers/azure/azapi/latest/docs)

`Tags: Microsoft.CognitiveServices/accounts/connections, Integration`
