# Teams Conversation Bot SSO quick-start

This samples demonstrates a Bot Framework SDK bot migrating to Agents SDK.

This sample demonstrates how to integrate Azure AD authentication in Microsoft Teams using a bot with Single Sign-On (SSO) capabilities. Built with the Co-Pilot SDK, it showcases OAuth SSO, Adaptive Cards, and Microsoft Graph API interactions. The sample includes reusable components, like the TeamsActivityHandler, for handling Invoke Activity in Teams. It provides a step-by-step setup guide to authenticate users with identity providers such as Microsoft Entra ID, GitHub, and others.

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents), it shows how to get started with SSO in a bot for Microsoft Teams.

The focus of this sample is how to use the Microsoft 365 Agents SDK support for OAuth SSO in your bot. Teams behaves slightly differently than other channels in this regard. Specifically an Invoke Activity is sent to the bot rather than the Event Activity used by other channels. _This Invoke Activity must be forwarded to the dialog if the OAuthPrompt is being used._ This is done by subclassing the ActivityHandler and this sample includes a reusable TeamsActivityHandler. This class is a candidate for future inclusion in the Agents SDK.

The sample uses the OAuth capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop a bot that authenticates users to various identity providers such as Microsoft Entra ID, GitHub, Uber, etc. The OAuth token is then used to make basic Microsoft Graph queries.

## Included Features
* Teams SSO
* Adaptive Card
* Graph API

![Teams Conversation Bot SSO Sample](Images/BotConversationSsoQuickStart.gif)

## Prerequisites

-  Microsoft Teams is installed and you have an account (not a guest account)
-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)
-  [M365 developer account](https://docs.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/prepare-your-o365-tenant) or access to a Teams account with the appropriate permissions to install an app.

## Running this sample

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Be sure to add the Teams Channel
   - Record the Application ID, the Tenant ID, and the Client Secret for use below

1. [Add OAuth to your bot](https://aka.ms/AgentsSDK-AddAuth)

1. Configuring the token connection in the Agent settings
   > The instructions for this sample are for a SingleTenant Azure Bot using ClientSecrets.  The token connection configuration will vary if a different type of Azure Bot was configured.  For more information see [DotNet MSAL Authentication provider](https://aka.ms/AgentsSDK-DotNetMSALAuth)

   1. Open the `appsettings.json` file in the root of the sample project.

   1. Find the section labeled `Connections`,  it should appear similar to this:

      ```json
      "Connections": {
          "ServiceConnection": {
          "Assembly": "Microsoft.Agents.Authentication.Msal",
          "Type":  "MsalAuth",
          "Settings": {
              "AuthType": "ClientSecret", // this is the AuthType for the connection, valid values can be found in Microsoft.Agents.Authentication.Msal.Model.AuthTypes.  The default is ClientSecret.
              "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
              "ClientId": "{{ClientId}}", // this is the Client ID used for the Azure Bot
              "ClientSecret": "00000000-0000-0000-0000-000000000000", // this is the Client Secret used for the connection.
              "Scopes": [
                "https://api.botframework.com/.default"
              ]
          }
      }
      ```

      1. Replace all **{{ClientId}}** to the AppId of the Agent.
      1. Repalce all **{{TenantId}}** to the Tenant Id where your application is registered.
      1. Set the **ClientSecret** to the Secret that was created for your identity.
      
      > Storing sensitive values in appsettings is not recommend.  Follow [AspNet Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0) for best practices.

1. Set "ConnectionName" in the `appsettings.json`. The Microsoft Entra ID ConnectionName from the OAuth Connection Settings on Azure Bot registration

1. Manually update the manifest.json
   - Edit the `manifest.json` contained in the `/appManifest` folder
     -  Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`
     - Replace `<<AGENT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.
   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
1. Upload the `manifest.zip` to Teams
   - Select **Developer Portal** in the Teams left sidebar
   - Select **Apps** (top row)
   - Select **Import app**, and select the manifest.zip

1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:
   > NOTE: Go to your project directory and open the `./Properties/launchSettings.json` file. Check the port number and use that port number in the devtunnel command (instead of 3978).

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

1. Start the Agent, and select **Preview in Teams** in the upper right corner

## Interacting with this Agent in Teams

![Install](Images/1.Install.png)

![bot signin card](Images/2.Installed.png)

![user details card](Images/3.Logged_In.png)

![token](Images/4.Your_Token.png)

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
