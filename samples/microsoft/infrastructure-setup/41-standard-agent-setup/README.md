# Azure AI Agent Service: Standard Agent Setup 1RP with Public Networking

> **NOTE:** This template is now supported

## Steps

See Instructions: https://microsoft-my.sharepoint.com/:w:/p/fosteramanda/ES-0A2WpCgVLrK3SH_7gT9YBBb8SZk639kKmU1AIpoeDJg?e=npAZWP

[![Deploy To Azure](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/deploytoazure.svg?sanitize=true)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fazure-ai-foundry%2Ffoundry-samples%2Frefs%2Fheads%2Fmain%2Fsamples%2Fmicrosoft%2Finfrastructure-setup%2F41-standard-agent-setup%2Fazuredeploy.json)

1. Create new (or use existing) resource group:

```bash
    az group create --name <new-rg-name> --location westus
```

2. Deploy the template

```bash
    az deployment group create --resource-group <new-rg-name> --template-file main.bicep
```

**Azure Cosmos DB provisioning notes**
When using the above template to create a new Azure Cosmos DB for NoSQL account, or if you use an existing account, note that 3 containers will be created and have an initial setup of 20,000 RU/s each. This is designed to handle very large agentic workflows, however, when just starting out we strongly recommend reducing this down to 2,000 RU/s on each container and using the Autoscale capability. [Learn more here](https://learn.microsoft.com/azure/cosmos-db/nosql/how-to-provision-autoscale-throughput?tabs=api-async).
