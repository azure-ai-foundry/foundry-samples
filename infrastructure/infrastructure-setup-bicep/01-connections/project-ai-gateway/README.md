# Project AI Gateway Enablement

This sample creates a new **Azure AI Foundry project** under an existing,
already **AI-Gateway-enabled** Foundry account and enables the AI Gateway on the
new project by default.

Unlike the other samples in [01-connections](../README.md) — which create a
`Microsoft.CognitiveServices/accounts/connections` resource — this sample
provisions the per-project **API Management (APIM)** artifacts that mark a
project as *gateway enabled*.

## What it deploys

1. **Project** (`Microsoft.CognitiveServices/accounts/projects`) under the
   existing account.
2. **APIM Product** (`subscriptionRequired: true`, `state: 'published'`) on the
   account's gateway APIM service.
3. **Product ↔ API association** — associates the gateway's shared API (named
   after the Foundry account) with the product.
4. **APIM Subscription** — an active subscription scoped to the product.
5. **ARM resource link** (`Microsoft.Resources/links`) from the project to the
   product. The presence of this link is what marks the project as **Enabled**
   on the gateway.

## Prerequisites

1. Azure CLI installed and configured.
2. An existing Azure AI Foundry account that already fronts an AI Gateway (i.e.
   an APIM service).
3. The full ARM resource id of the APIM service that backs the account gateway.

## Parameters

| Parameter | Required | Description |
| --- | --- | --- |
| `aiFoundryAccountName` | Yes | Name of the existing gateway-enabled Foundry account. |
| `projectName` | Yes | Name of the new project to create. |
| `apimResourceId` | Yes | Full ARM resource id of the APIM service backing the account gateway. |
| `projectDisplayName` | No | Display name for the project. Defaults to `projectName`. |
| `projectDescription` | No | Description for the project. |
| `location` | No | Location for the project. Defaults to the resource group location. |
| `sharedApiId` | No | Gateway shared API id to associate with the product. Defaults to the account name (lowercased). |

## How to Deploy

```bash
# 1. Edit samples/parameters.json with your account name, project name, and APIM resource id
# 2. Deploy into the resource group / subscription where the Foundry account lives
az deployment group create \
  --resource-group <foundry-rg> \
  --template-file project-ai-gateway.bicep \
  --parameters @samples/parameters.json
```

## Outputs

| Output | Description |
| --- | --- |
| `projectResourceId` | Resource ID of the newly created project. |
| `projectPrincipalId` | System-assigned principal ID of the new project. |
| `productName` | Name of the per-project APIM product created on the gateway. |
| `gatewayProductScope` | Resource-link target (the APIM product) that marks the project Enabled. |
