# OBO OAuth

This Agent has been created using [Microsoft 365 Agents Framework](https://github.com/microsoft/agents-for-net), it shows how to use authorization in your Agent using OAuth and OBO.

- The sample uses the Agent SDK User Authorization capabilities in [Azure Bot Service](https://docs.botframework.com), providing features to make it easier to develop an Agent that authorizes users with various identity providers such as Azure AD (Azure Active Directory), GitHub, Uber, etc.
- This sample shows how to use an OBO Exchange to communicate with Microsoft Copilot Studio using the CopilotStudioClient.

- ## Prerequisites

-  [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0
-  [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows)

## Running this sample

1. Create a Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
   1. Publish your newly created Copilot
   1. Go to Settings => Advanced => Metadata and copy the following values. You will need them later:
      1. Schema name
      1. Environment Id
       
2. Create an Azure Bot with one of these authentication types
   - [SingleTenant, Client Secret](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md)

3. Setting up OAuth for an exchangeable token 
   1. Got to the App Registration for the Azure Bot
      1. **Authentication** tab
         1. **Add Platform**, then **Web**, Set `Redirect URI` to `Web` and `https://token.botframework.com/.auth/web/redirect`
         1. **Add Platform**, then **Mobile and desktop applications**, and add an additional `http://localhost` Uri.
      1. **API Permissions** tab
         1. **Dynamics CRM** with **user_impersonation**
         1. **Graph** with **User.Read**
         1. **Power Platform API** with **CopilotStudio.Copilots.Invoke**
         1. Grant Admin Consent for your tenant.
      1. **Expose an API** tab
         1. Click **Add a Scope**
         1. **Application ID URI** should be: `api://botid-{{appid}}`
         1. **Scope Name** is "defaultScope"
         1. **Who can consent** is **Admins and users**
         1. Enter values for the required Consent fields
      1. **Certificates & secrets**
         1. Create a new secret and record the value.  This will be used later.
         
4. Create Azure Bot **OAuth Connection**
   1. On the Azure Bot, click **Configuration** tab then the **Add OAuth Connection Settings** button.
   1. Enter a **Name**.  This will be used later.
   1. For **Service Provider** select **Azure Active Directory v2**
   1. **Client id** and **Client Secret** are the values created in step #2.
   1. Enter the **Tenant ID**
   1. **Scopes** is `api://botid-{{appid}}/defaultScope`

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
      
   1. In appsettings, replace **{{AzureBotOAuthConnectionName}}** with the OAuth Connection Name created in Step #4.

   1. Setup the Copilot Studio Agent information
      ```json
      "CopilotStudioAgent": {
        "EnvironmentId": "", // Environment ID of environment with the CopilotStudio App.
        "SchemaName": "", // Schema Name of the Copilot to use
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

1. ## Enabling JWT token validation
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

