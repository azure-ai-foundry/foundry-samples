---
description: This template deploys an Azure AI Foundry account, project, model deployment, and Application Insights tracing with your User-Assigned Managed Identity.
page_type: sample
products:
- azure
- azure-resource-manager
urlFragment: aifoundry-uai
languages:
- bicep
- json
---
# Set up Azure AI Foundry with user-assigned identity

This Azure AI Foundry template is built on Azure Cognitive Services as a resource provider. It deploys:

- An Azure AI Foundry account and project
- A GPT-4o model deployment
- A Log Analytics workspace
- A workspace-based Application Insights component
- An Application Insights connection on the project
- Monitoring roles for the user-assigned managed identity

By default, the template creates a user-assigned managed identity. To reference an existing identity, provide its name and resource group. The project uses the Application Insights connection string to authenticate trace ingestion by default. The template assigns Log Analytics Reader and Privileged Monitoring Data Reader to the identity so the project can query traces, including generative AI content.

To use the project managed identity for trace ingestion instead, set `useProjectManagedIdentityForTraceIngestion=true`. The template then assigns the identity the Monitoring Metrics Publisher role.

Run the Bicep deployment:

```azurecli
az deployment group create \
  --name "{DEPLOYMENT_NAME}" \
  --resource-group "{RESOURCE_GROUP_NAME}" \
  --template-file ./main.bicep \
  --parameters userAssignedIdentityName="{USER_ASSIGNED_MANAGED_IDENTITY_NAME}"
```

To use an existing user-assigned managed identity:

```azurecli
az deployment group create \
  --name "{DEPLOYMENT_NAME}" \
  --resource-group "{RESOURCE_GROUP_NAME}" \
  --template-file ./main.bicep \
  --parameters \
    userAssignedIdentityName="{USER_ASSIGNED_MANAGED_IDENTITY_NAME}" \
    userIdentityResourceGroupName="{IDENTITY_RESOURCE_GROUP_NAME}"
```

To use the project managed identity for trace ingestion:

```azurecli
az deployment group create \
  --name "{DEPLOYMENT_NAME}" \
  --resource-group "{RESOURCE_GROUP_NAME}" \
  --template-file ./main.bicep \
  --parameters \
    userAssignedIdentityName="{USER_ASSIGNED_MANAGED_IDENTITY_NAME}" \
    useProjectManagedIdentityForTraceIngestion=true
```

Deploying the role assignments requires permission to create role assignments at the Application Insights resource scope.

Limitations:

1. User-assigned managed identity is not supported with customer-managed keys.
2. A project can have only one Application Insights connection.

If you are new to Azure AI Foundry, see:

- [Azure AI Foundry](https://learn.microsoft.com/azure/ai-foundry/)

If you are new to template deployment, see:

- [Azure Resource Manager documentation](https://learn.microsoft.com/azure/azure-resource-manager/)
- [Azure AI services quickstart article](https://learn.microsoft.com/azure/cognitive-services/resource-manager-template)

`Tags: Microsoft.CognitiveServices/accounts/projects`