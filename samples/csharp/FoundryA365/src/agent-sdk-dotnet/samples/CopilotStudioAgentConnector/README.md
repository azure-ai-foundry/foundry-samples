# CopilotStudioAgentConnector

- This sample responds to Power Apps Connector requests from Copilot Studio.
- This sample shows how to use an OBO Exchange to read from Graph to get their name and respond with "Hi, {{name}}"

## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)


## Running this sample

1. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md)
   - [SingleTenant, Federated Credentials](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-fic.md) 

3. Configuring the authentication connection in the Agent settings
   > These instructions are for **SingleTenant, Client Secret**. For other auth type configuration, see [DotNet MSAL Authentication](https://github.com/microsoft/Agents/blob/main/docs/HowTo/MSALAuthConfigurationOptions.md).
   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections` and `TokenValidation`,  it should appear similar to this:

      ```json
      "TokenValidation": {
        "Enabled": true,
        "Audiences": [
            "{{ClientId}}"
        ],
        "TenantId": "{{TenantId}}"
      },

      "Connections": {
        "ServiceConnection": {
          "Settings": {
            "AuthType": "ClientSecret",          // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
            "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
            "ClientId": "{{ClientId}}",          // this is the Client ID used for the connection.
            "ClientSecret": "{{ClientSecret}}"   // this is the Client Secret used for the connection.
          }
        }
      },
      ```

      1. Replace all **{{ClientId}}** with the AppId of the Azure Bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **{{ClientSecret}}** to the Secret that was created on the App Registration.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

 
4. Add the MCS Connector Agent
   - TBD
 
## Further reading
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
