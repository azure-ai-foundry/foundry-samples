# User Authorization Samples

These Samples demonstrate 3 methods of acquiring User tokens from inside an agent.  Each method has specific use cases where they can be applied.

| Type of Authorization Flow | Sample Name | Description
|----------------------------|-------------|----------------------------------------------------
| Automatic Sign In| AutoSignIn | This sample demonstrates the use of the AgentSDK's built in Automatic Sign in Flow.  This Flow triggers anytime a message request is submitted ot the Agent.
| Manual Sign In| ManualSignIn | This sample demonstrates the use of an on-demand login request. This request only logs in if the user or code requests it.
| On Behalf of| OBOSignIn | This Sample utilizes the Automatic Sign In and Token Exchange.

# User Authorization Samples General Setup

>[!Warning]
> It's important you follow these instructions prior to running the samples as this contains the common setup needed by all samples.

These Samples use the OAuth capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop a bot that authorizes users to various identity providers such as Entria ID (formally Azure Active Directory), GitHub, Uber, etc.

These samples are designed to though Azure Bot Services, are are not intended to work with the Bot Framework Emulator, Teams Test Tool at this time. 

## Prerequisites

Access and Software
- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
- Access to create Registrations on Azure Bot Service
- Access to create Entra ID Application Registrations
- (Optional) Access to deploy M365\Teams Applications

### Setup Entra Applications 

For these samples you will need to create two Entra ID Application Registrations.  
- Agent Application Identity
  - This identity will be used by your Agent and Azure Bot Services to Communicate with each other
- User Application Identity
  - This is used as the broker application that will be used for end user authentication, API permissions and provide the token for the user to the Agent.

#### Create the Agent Application Identity

You if you have an exiting Bot Service Agent Registration that you want to reuse and you have the Application(client) ID, Tenant ID, and Client Secret, you can skip this step.  Otherwise, follow the steps below to create a new Agent Application Registration.

For the purposes of local development we will use an application registration that is registered in the same tenant as the Azure Bot Service.  This is not a requirement, but it does make local development easier. We will also 
1. Create a new Entra ID Application Registration
1. Go to the [Azure Portal](https://portal.azure.com)
1. Select **Microsoft Entra ID** from the Azure Services List
1. Select **App registrations** in the left menu
   - Name: `AgentApp`
   - Supported Account Types: `Accounts in this organizational directory only (Single tenant)`
   - Click **Register**
   - Record the Agent Application ID (Client ID) and Directory (Tenant ID) for use below
   - Click **Authentication** in the left menu
   - 

This Agent has been created using [Microsoft 365 Agents Framework](https://github.com/microsoft/agents-for-net), it shows how to use user authorization in your Agent.


- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## Running this sample

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. [Add OAuth to your bot](https://aka.ms/AgentsSDK-AddAuth)

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "ConnectionName": "{{ConnectionName}}",
   
      "TokenValidation": {
        "Audiences": [
          "{{ClientId}}" // this is the Client ID used for the Azure Bot
        ],
        "TenantId": "{{TenantId}}"
      },

      "Connections": {
          "ServiceConnection": {
          "Assembly": "Microsoft.Agents.Authentication.Msal",
          "Type":  "MsalAuth",
          "Settings": {
              "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
              "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
              "ClientId": "{{ClientId}}", // this is the Client ID used for the connection.
              "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
              "Scopes": [
                "https://api.botframework.com/.default"
              ]
          }
      }
      ```

      1. Replace all **{{ClientId}}** with the AppId of the bot.
      1. Replace all **{{TenantId}}** with the Tenant Id where your application is registered.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Update `appsettings.json` 

   | Property             | Value Description     | 
   |----------------------|-----------|
   | ConnectionName       | Set the configured bot's OAuth connection name.      |
    
1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and update it to match your DevTunnel port. If `./Properties/launchSettings.json`not fount Close and re-open the solution.launchSettings.json have been re-created.

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. Update your Azure Bot ``Messaging endpoint`` with the tunnel Url:  `{tunnel-url}/api/messages`

1. Run the bot from a terminal or from Visual Studio

1. Test via "Test in WebChat"" on your Azure Bot in the Azure Portal.

## Running this Agent in Teams

1. There are two version of the manifest provided.  One for M365 Copilot and one for Teams.
   1. Copy the desired version to manifest.json
1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<AGENT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Select **Preview in Teams** in the upper right corner

## Interacting with the Agent

Type anything to sign-in, or `logout` to sign-out.  

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.

