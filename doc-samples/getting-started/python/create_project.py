# source: https://github.com/Azure/agent-first-sdk/blob/main/tests/management_sdk/manage_ai_foundry.ipynb

# <create_project>
from azure.identity import DefaultAzureCredential
from azure.mgmt.cognitiveservices import CognitiveServicesManagementClient

subscription_id = "your-subscription-id"
resource_group_name = "your-resource-group-name"
foundry_resource_name = "your-foundry-resource-name"
foundry_project_name = "your-foundry-project-name"
location = "eastus"

# TODO: add code to create create a new resource group

client = CognitiveServicesManagementClient(
    subscription_id=subscription_id, credential=DefaultAzureCredential(), api_version="2025-04-01-preview"
)

account = client.accounts.begin_create(
    resource_group_name=resource_group_name,
    account_name=foundry_resource_name,
    foundry_project_name=foundry_project_name,
    account={
        "location": location,
        "kind": "AIServices",
        "sku": {
            "name": "S0",
        },
        "identity": {"type": "SystemAssigned"},
        "properties": {"allowProjectManagement": True},
    },
)

# TODO: code to do role assignment to give user project manager role on the account

# </create_project>

# <deploy_model>

# </deploy_model>
