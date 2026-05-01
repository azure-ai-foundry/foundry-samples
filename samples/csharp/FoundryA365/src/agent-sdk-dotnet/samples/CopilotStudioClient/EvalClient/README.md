# Copilot Studio Agent Evaluation Sample

## Instructions - Setup

### Prerequisite

To setup for this sample, you will need the following:

1. A RAG Agent Created in Microsoft Copilot Studio, or access to an existing RAG Agent.
1. Ability to Create a Application Identity in Azure for a Public Client/Native App Registration Or access to an existing Public Client/Native App registration with the CopilotStudio.Copilot.Invoke API Permission assigned.
1. An Azure OpenAI Resource with a Deployment of either the `gpt-35-turbo` or `gpt-4o` model.

### Create a Agent in Copilot Studio

1. Create a Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
    1. Publish your newly created Copilot
    1. Goto Settings => Advanced => Metadata and copy the following values, You will need them later:
        1. Schema name
        1. Environment Id

### Create an Application Registration in Entra ID

This step will require permissions to Create application identities in your Azure tenant. For this sample, you will be creating a Native Client Application Identity, which does not have secrets.

1. Open https://portal.azure.com
1. Navigate to Entra Id
1. Create an new App Registration in Entra ID
    1. Provide an Name
    1. Choose "Accounts in this organization directory only"
    1. In the "Select a Platform" list, Choose "Public Client/native (mobile & desktop)
    1. In the Redirect URI url box, type in `http://localhost` (**note: use HTTP, not HTTPS**)
    1. Then click register.
1. In your newly created application
    1. On the Overview page, Note down for use later when configuring the example application:
        1. the Application (client) ID
        1. the Directory (tenant) ID
    1. Goto Manage
    1. Goto API Permissions
    1. Click Add Permission
        1. In the side pannel that appears, Click the tab `API's my organization uses`
        1. Search for `Power Platform API`.
            1. *If you do not see `Power Platform API` see the note at the bottom of this section.*
        1. In the permissions list choose `CopilotStudio` and Check `CopilotStudio.Copilots.Invoke`
        1. Click `Add Permissions`
    1. (Optional) Click `Grant Admin consent for copilotsdk`
    1. Close Azure Portal

### Create an Azure OpenAI Resource

1. Open https://portal.azure.com
1. Click "+ Create a Resource"
1. Search for "Azure OpenAI" and click "Create"
1. Provide the resource details:
   1. Subscription: Select your subscription
   1. Resource Group: Select or create a resource group
   1. Region: Select a region
   1. Name: Provide a name for the resource
   1. Pricing Tier: Select a pricing tier
   1. Click "Next"
   1. In the network tab, select "All networks, including the internet, can access this."
1. Select "Create"
1. Navigate to the newly created Azure OpenAI resource and under "Keys and Endpoint" note down the following values:
   1. KEY 1
   1. Endpoint
1. Explore the Aure OpenAI endpoint in Azure AI Foundry and from the Model Catalog, deploy a "gpt-40" or "gpt-35-turbo" model.
   1. Note down the deployment name.

> [!TIP]
> If you do not see `Power Platform API` in the list of API's your organization uses, you need to add the Power Platform API to your tenant. To do that, goto [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) and follow the instructions on Step 2 to add the Power Platform Admin API to your Tenant

## Instructions - Configure the Example Application

With the above information, you can now run the client `CopilostStudioClientSample`.

1. Open the appSettings.json file for the CopilotStudioClientSample, or rename launchSettings.TEMPLATE.json to launchSettings.json.
1. Configured the values for the various key's based on what was recorded during the setup phase.

```json
"DirectToEngineSettings": {
  "EnvironmentId":         "", // Environment ID of environment with the CopilotStudio App.
  "SchemaName":            "", // Schema Name of the Copilot to use
  "TenantId":              "", // Tenant ID of the App Registration used to login,  this should be in the same tenant as the Copilot.
  "AppClientId":           "", // App ID of the App Registration used to login,  this should be in the same tenant as the Copilot.
  "AzureOpenAiEndpoint":   "", // Azure OpenAI Endpoint
  "AzureOpenAiKey":        "", // Azure OpenAI Key
  "AzureOpenAiDeployment": ""  // Azure OpenAI Deployment
}
```

3. Edit the file `./Data/Evaluation Dataset.csv` and add the questions, ground truths and source links that you want to evaluate.

4. Build and run the EvalClient.exe program.

This should challenge you for login and connect ot the Copilot Studio Hosted Agent. Then, it runs all the evaluations in the evaluation dataset and stores the results in the `.\bin\net9.0\Data\Evaluation Dataset Results.csv` file.

## Sample Evaluation

### Evaluation Dataset.csv

```plain
Name: Asus Zenbook Duo 2024
Test Utterance: How can I charge the removable keyboard of the Asus Zenbook Duo 2024?
Expected Response: To charge the removable keyboard of the Asus Zenbook Duo 2024 insert the bundled power adapter into the USB-C port on the keyboard or dock it with the laptop to charge via the built-in connector.
Sources: https://tenant.sharepoint.com/sample/Document%20Library%201/2.pdf;https://tenant.sharepoint.com/sample/Document%20Library%201/3.pdf
```

### Evaluation Dataset Results.csv

```plain
Name: Asus Zenbook Duo 2024
est Utterance: How can I charge the removable keyboard of the Asus Zenbook Duo 2024?
Expected Response: To charge the removable keyboard of the Asus Zenbook Duo 2024 insert the bundled power adapter into the USB-C port on the keyboard or dock it with the laptop to charge via the built-in connector.
Sources: https://tenant.sharepoint.com/sample/Document%20Library%201/2.pdf;https://tenant.sharepoint.com/sample/Document%20Library%201/3.pdf
Agent Response: 
To charge the removable keyboard of the Asus Zenbook Duo 2024 you can follow these steps:
1. Connect the AC power cord to the AC/DC adapter.
2. Connect the DC power connector into your keyboard’s power (DC) input port.
3. Plug the AC power adapter into a 100V~240V power source [1].
[1]: https://tenant.sharepoint.com/sample/Document%20Library%201/2.pdf
Answer Score: 65
Sources Score: 1/2
```

### Explanation

This RAG agent was created by adding a SharePoint knowledge source to the agent. The evaluator client asks the agent to answer the question provided in the "Test Utterance" 
field and compares the answer with the "Expected Response" answer. Based on how well the agent's answer matches the expected response semantically, a score is given 
between 0 and 100, 10 being the worst and 100 the perfect answer. The value is stored in the "Answer Score" field. The "Sources" field provided 2 URLs the agent should have used to answer the question. 
The evaluator client checks if the agent used the provided URLs to answer the question by returning them as reference links and returns it as x/y where x are the number of links provided by the agent 
over y which represents the number of links expected.

## Authentication

The EvalClient requires a User Token to operate. For this sample, we are using a user interactive flow to get the user token for the application ID created above.

The Copilot client will use a named `HttpClient` retrieved from the `IHttpClientFactory` as `mcs` injected in DI. This client needs to be configured with a `DelegatingHandler` to apply a valid Entra ID Token. In this sample using MSAL.
