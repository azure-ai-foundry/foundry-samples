# Azure AI Agent Service: Standard Agent Setup 1RP with Private E2E Networking 

> **NOTE:** This template is to set-up a network secured Standard Agent in AI Foundry. Includes: 
* PNA disabled resources
* PE's to all resources
* Network injection enabled for Agents
* Virtual Network Address speace support for Class B or Class C e.g. 172.16.0.0/16 or 192.168.0.0/16


## Steps 

1. Create new (or use existing) resource group:

```bash
    az group create --name <new-rg-name> --location <your-rg-region>
```

2. Deploy the main-create.bicep

```bash
    az deployment group create --resource-group <new-rg-name> --template-file main-create.bicep
```
3. Run the CheckCapabilityHostReadiness.ps1 and edit the command to add your subscription ID, resource group name, and your newly deployed AI Services account resource name.

```bash
    .\CheckCapabilityHostReadiness.ps1 -subscriptionId "<your-sub-id>" -resourcegroup "<new-rg-name>" -accountname "<your-aiservices-name>"
```

If you do not want to run the Powershell script, you can run a bash script instead, from the file CheckCapabilityHostReadiness.sh. Run the following two commands:

```bash
    chmod +x CheckCapabilityHostReadiness.sh
    ./CheckCapabilityHostReadiness.sh "<your-sub-id>" "<new-rg-name>" "<your-aiservices-name>"
```

4. Deploy the main-project-caphost-create.bicep

```bash
    az deployment group create --resource-group <new-rg-name> --template-file main-project-caphost-create.bicep
```

After running this script, you are required to input the following values:

```
    Please provide string value for 'accountName' (? for help): <your-account-name>
    Please provide string value for 'projectName' (? for help): <your-project-name>
    Please provide string value for 'aiSearchName' (? for help): <your-search-name>
    Please provide string value for 'azureStorageName' (? for help): <your-storage-name>
    Please provide string value for 'cosmosDBName' (? for help): <your-cosmosdb-name>
```

**NOTE:** To access your Foundry resource securely, please using either a VM, VPN, or ExpressRoute.