# Microsoft Copilot Studio Skill Sample

This is a sample of a simple conversational Agent that can be used from the Microsoft Copilot Studio Skills feature.

This sample is intended to introduce you to:
- The basic operation of the Microsoft 365 Agents SDK messaging handling.
- Requirements of a conversational Agent being used by another Agent.
- Using this Agent from Microsoft Copilot Studio.

## Basic concepts
- A Skill is an Agent.  There is nothing inherently different from other Agents.
- While many Agents are conversational, they can respond to any messages sent using the Activity Protocol.
- Conversations are multi-turn interactions, including possibly a large number of exchanges with the user until the conversation is complete.  
- Agents being called by another Agent are expected to:
  - Indicate when the conversation is over, with a "success" result and optional return value.
  - Indicate the conversation is over after a critical error.
- Microsoft Copilot Studio requires a manifest that describes the Agent capabilities.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## Using this Agent from Microsoft Copilot Studio as a Skill

- In order to use this sample from Copilot Studio, the Agent will need to be created on Azure, and authentication setup.
  - It is possible to run the Agent locally for debugging purposes.  
  - Managed Identity does not work locally so `ClientSecret` or `Certificate` will need to be used.
  - A tunnel will be required to route messages sent to the Agent to you locally running agent.
- Copilot Studio requires a manifest for the agent ("Skills Manifest").  This is discussed below.

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

1. Updating the Agent Manifest
   - Open the [sample manifest](./wwwroot/manifest/echoskill-manifest-1.0.json)
   - Update the `privacyUrl`, `iconUrl`, `msAppId`, and `endpointUrl`
   - If you are running the Agent locally, this will be `{tunnel-url}/api/messages`
   - Once EchoSkill is started, the manifest is available via a GET to `{host-or-tunnel-url}/manifest/echoskill-manifest-1.0.json`

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

### Using EchoSkill in Copilot Studio
- In order to use an Agent from Copilot Studio Skills
  - The Azure Bot and Identity must have been created on Azure
  - If the Agent is running locally, the **Messaging Endpoint** on the Azure Bot must be set to `{tunnel-url}/api/messages`
  - On the App Registration on Azure, the "Home page URL" should be set to the same as **Messaging Endpoint**
- Create a new, or open an existing, Copilot Studio Agent
- Go to Agent **Settings**
- Select **Skills** on the left sidebar
- Select **Add a Skill**
- Enter the URL to the skill manifest, for example `{host-or-tunnel-url}/manifest/echoskill-manifest-1.0.json`
- Click **Save** when validation is complete.
- From any Topic, add a new **Call an Action** node, and select "Echo messages from user"
- Test the agent in Copilot Studio.

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
To learn more about building Agents, see our [Microsoft Agents Framework on GitHub](https://github.com/microsoft/agents) repo.
