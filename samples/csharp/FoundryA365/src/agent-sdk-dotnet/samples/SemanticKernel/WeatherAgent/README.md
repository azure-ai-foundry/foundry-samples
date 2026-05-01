# WeatherBot Sample with Semantic Kernel

This is a sample of a simple Weather Forecast Agent that is hosted on an Asp.net core web service.  This Agent is configured to accept a request asking for information about a weather forecast and respond to the caller with an Adaptive Card.

This Agent Sample is intended to introduce you the basics of integrating Semantic Kernel with the Microsoft 365 Agents SDK in order to build powerful Agents. It can also be used as a the base for a custom Agent that you choose to develop.

***Note:*** This sample requires JSON output from the model which works best from newer versions of the model such as gpt-4o-mini.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit)

## QuickestStart using Agent Toolkit
1. If you haven't done so already, install the Agents Playground
 
   ```
   winget install agentsplayground
   ```
1. Configure OpenAI in appsettings
   - You will need an Azure OpenAI or OpenAI instance, with the preferred model of `gpt-4o-mini`.   
    
     ```json
     "AIServices": {
       "AzureOpenAI": {
         "DeploymentName": "", // This is the Deployment (as opposed to model) Name of the Azure OpenAI model
         "Endpoint": "", // This is the Endpoint of the Azure OpenAI model deployment
         "ApiKey": "" // This is the API Key of the Azure OpenAI model deployment
       },
       "OpenAI": {
         "ModelId": "", // This is the Model ID of the OpenAI model
         "ApiKey": "" // This is the API Key of the OpenAI model
       },
       "UseAzureOpenAI": true // This is a flag to determine whether to use the Azure OpenAI model or the OpenAI model  
     }
     ```
1. Start the Agent in VS or VS Code in debug
1. Start Agents Playground.  At a command prompt: `agentsplayground`
   - The tool will open a web browser showing the Microsoft 365 Agents Playgroun, ready to send messages to your agent. 
1. Interact with the Agent via the browser

### QuickStart using WebChat

- Overview of running and testing an Agent
  - Provision an Azure Bot in your Azure Subscription
  - Configure your Agent settings to use to desired authentication type
  - Running an instance of the Agent app (either locally or deployed to Azure)
  - Test in a client

1. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md)
   - [SingleTenant, Federated Credentials](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-fic.md) 
   - [User Assigned Managed Identity](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-msi.md)

1. Configuring the authentication connection in the Agent settings
   > These instructions are for **SingleTenant, Client Secret**. For other auth type configuration, see [DotNet MSAL Authentication](https://github.com/microsoft/Agents/blob/main/docs/HowTo/MSALAuthConfigurationOptions.md).
   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{ClientId}}", // this is the Client ID used for the connection.
            "ClientSecret": "{{ClientSecret}}", // this is the Client Secret used for the connection.
            "Scopes": [
              "https://api.botframework.com/.default"
            ]
          }
        }
      },
      ```

      1. Replace all **{{ClientId}}** with the AppId of the Azure Bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **{{ClientSecret}}** to the Secret that was created on the App Registration.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Configure OpenAI in appsettings
   - You will need an Azure OpenAI or OpenAI instance, with the preferred model of `gpt-4o-mini`.   
    
     ```json
     "AIServices": {
       "AzureOpenAI": {
         "DeploymentName": "", // This is the Deployment (as opposed to model) Name of the Azure OpenAI model
         "Endpoint": "", // This is the Endpoint of the Azure OpenAI model deployment
         "ApiKey": "" // This is the API Key of the Azure OpenAI model deployment
       },
       "OpenAI": {
         "ModelId": "", // This is the Model ID of the OpenAI model
         "ApiKey": "" // This is the API Key of the OpenAI model
       },
       "UseAzureOpenAI": true // This is a flag to determine whether to use the Azure OpenAI model or the OpenAI model  
     }
     ```

1. Running the Agent
   1. Running the Agent locally
      - Requires a tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.
      - **For ClientSecret or Certificate authentication types only.**  Federated Credentials and Managed Identity will not work via a tunnel to a local agent and must be deployed to an App Service or container.
      
      1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

         ```bash
         devtunnel host -p 3978 --allow-anonymous
         ```

      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

      1. Start the Agent in Visual Studio

   1. Deploy Agent code to Azure
      1. VS Publish works well for this.  But any tools used to deploy a web application will also work.
      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `https://{{appServiceDomain}}/api/messages`

## Testing this agent with WebChat

   1. Select **Test in WebChat** on the Azure Bot

## Testing this Agent in Teams or M365

1. Update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
     - `manifest.json`
     - `outline.png`
     - `color.png`

1. Your Azure Bot should have the **Microsoft Teams** channel added under **Channels**.

1. Navigate to the Microsoft Admin Portal (MAC). Under **Settings** and **Integrated Apps,** select **Upload Custom App**.

1. Select the `manifest.zip` created in the previous step. 

1. After a short period of time, the agent shows up in Microsoft Teams and Microsoft 365 Copilot.

## Enabling JWT token validation
1. By default, the AspNet token validation is disabled in order to support local debugging.
1. Enable by updating appsettings
   ```json
   "TokenValidation": {
     "Enabled": false,
     "Audiences": [
       "{{ClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Further reading
To learn more about building Bots and Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
