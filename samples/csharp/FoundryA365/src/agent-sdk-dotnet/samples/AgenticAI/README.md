# AgenticAI Sample

This is a base sample that responds in the Teams demo env for Agentic AI.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit)

## QuickStart using Teams

- Overview of running and testing an Agent
  - The Agentic App has already been created.  You will need that ClientId, TenantId, and secret (if neededing to run locally).
  - Configure your Agent settings to use to desired authentication type
  - Running an instance of the Agent app (either locally or deployed to Azure)
  - Test in the Kairo Teams web client

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
            "TenantId": "{{TenantId}}"
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
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.