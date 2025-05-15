# Azure AI Agent Service: Standard Agent Setup 1RP with Public Networking

> **NOTE:** This template is now supported for PNA disabled account with PE and no network injection

## Steps 

See Instructions: https://microsoft-my.sharepoint.com/:w:/p/fosteramanda/ES-0A2WpCgVLrK3SH_7gT9YBBb8SZk639kKmU1AIpoeDJg?e=npAZWP


1. Create new (or use existing) resource group:

```bash
    az group create --name <new-rg-name> --location westus
```

2. Deploy the template

```bash
    az deployment group create --resource-group <new-rg-name> --template-file main.bicep
```
