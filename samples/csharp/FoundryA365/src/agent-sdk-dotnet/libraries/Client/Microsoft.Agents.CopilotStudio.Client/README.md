# Microsoft.Agents.CopilotStudio.Client

 Provides a client to interact with agents built in Copilot Studio. This Library is intended to provide access to a given agent's conversational channel.
 
 ## Changelog
| Version | Date | Changelog |
|------|----|------------|
| 1.2.0 | 2025-08-19 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/releases/tag/v1.2.0) |
| 1.3.0 | 2025-10-22 | [Detailed Changelog](https://github.com/microsoft/Agents-for-net/blob/main/changelog.md) |

 ## Instructions - Required Setup to use this library
 
 ### Prerequisite
 
 To use this library, you will need the following:
 
 1. An Agent Created in Microsoft Copilot Studio.
 1. Ability to Create or Edit an Application Identity in Azure 
    1. (Option 1) for a Public Client/Native App Registration or access to an existing registration (Public Client/Native App) that has the **CopilotStudio.Copilot.Invoke API Delegated Permission assigned**.
    1. (Option 2) for a Confidential Client/Service Principal App Registration or access to an existing App Registration (Confidential Client/Service Principal) with the **CopilotStudio.Copilot.Invoke API Application Permission assigned**.
 
 ### Create a Agent in Copilot Studio
 
 1. Create or open an Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
     1. Make sure that the Copilot is Published
     1. Goto Settings => Advanced => Metadata and copy the following values. You will need them later:
         1. Schema name - this is the 'unique name' of your agent inside this environment.
         1. Environment Id - this is the ID of the environment that contains the agent.
 
 ### Create an Application Registration in Entra ID to support user authentication to Copilot Studio
 
 > [!IMPORTANT]
 > If you are using this client from a service, you will need to exchange the user token used to login to your service for a token for your agent hosted in copilot studio. This is called a On Behalf Of (OBO) authentication token.  You can find more information about this authentication flow in [Entra Documentation](https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/web-apps-apis/on-behalf-of-flow). 
 > 
 > When using this method, you will need to add the `CopilotStudio.Copilots.Invoke` *delegated* API permision to your application registration's API privilages
  
### Add the CopilotStudio.Copilots.Invoke permissions to your Application Registration in Entra ID to support User or Service Principal authentication to Copilot Studio
 
 This step will require permissions to edit application identities in your Azure tenant.

 1. In your azure application
     1. Goto Manage
     1. Goto API Permissions
     1. Click Add Permission
         1. In the side pannel that appears, Click the tab `API's my organization uses`
         1. Search for `Power Platform API`.
             1. *If you do not see `Power Platform API` see the note at the bottom of this section.*
         1. For *User Interactive Permissions*, choose `Delegated Permissions`
            1. In the permissions list choose `CopilotStudio` and Check `CopilotStudio.Copilots.Invoke`
            1. Click `Add Permissions`
         1. For *Service Principal/Confidential Client*, choose `Application Permissions`
            1. In the permissions list choose `CopilotStudio` and Check `CopilotStudio.Copilots.Invoke`
            1. Click `Add Permissions`
            1. A appropiate administrator must then `Grant Admin consent for copilotsdk` before the permissions will be available to the application.
     1. Close Azure Portal
 
 > [!TIP]
 > If you do not see `Power Platform API` in the list of API's your organization uses, you need to add the Power Platform API to your tenant. To do that, goto [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) and follow the instructions on Step 2 to add the Power Platform Admin API to your Tenant
 
 
 ## How-to use
 
 ### Setting up configuration for the CopilotStudio Client

 The Copilot Client is configured using the `CopilotClientSettings` class.
 The `ConnectionSettings` class can be configured using either the default constructor or a parameterized constructor that accepts an `IConfigurationSection`. Below are the steps to configure an instance of the `ConnectionSettings` class.

 #### Using the Default Constructor
 
 You can create an instance of the `ConnectionSettings` class with default values using the default constructor. You can create the settings object using the the default constructor or via an IConfiguraition entry.

 There are a few options for configuring the `ConnectionSettings` class. The following are the most *common* options:

 Using Envrionment ID and Copilot Studio Agent Schema Name:
 ```csharp
var connectionSettings = new ConnectionSettings 
{ 
    EnvironmentId = "your-environment-id", 
    SchemaName = "your-agent-schema-name", 
};
 ```

 Using the DirectConnectUrl:
 ```csharp
var connectionSettings = new ConnectionSettings 
{
    DirectConnectUrl = "https://direct.connect.url", 
};
 ```

 > [!NOTE]
 > By default, its asumed your agent is in the Microsoft Public Cloud. If you are using a different cloud, you will need to set the `Cloud` property to the appropriate value. See the `PowerPlatformCloud` enum for the supported values
 > 

 #### Using an IConfigurationSection

 You can create an instance of the `ConnectionSettings` class using an `IConfigurationSection` object. 

 The following are the most *common* options:
 
 Using Envrionment ID and Copilot Studio Agent Schema Name:
 
```json
{
    "ConnectionSettings": {
        "EnvironmentId": "your-environment-id",
        "SchemaName": "your-agent-schema-name",
    }
}
 ```
 Using the DirectConnectUrl:

 ```json
{
    "ConnectionSettings": {
        "DirectConnectUrl": "https://direct.connect.url",
    }
}
```
 > [!NOTE]
 > By default, its asumed your agent is in the Microsoft Public Cloud. If you are using a different cloud, you will need to set the `Cloud` property to the appropriate value. See the `PowerPlatformCloud` enum for the supported values
 > 

 ### Getting an AccessToken for the Copilot Studio Client API
 
 >[!Important] User based authentication flows are currently supported for this client, Service Prinipal Flows are in private prieview and not documented in this release. 
 
 Your code will need to create a User Token ( via MSAL Public Client or an OBO flow for a User access token) to call this service, the application you use to do this must have the `CopilotStudio.Copilots.Invoke` permission assigned to it.
 
 There are currently two ways to pass auth to the CopilotClient.
 
 #### 1. Create an HttpMessageRequest Handler that will populate the bearer token and assign it to the httpClient you create
 
 In this case, you would create an HttpMessageRequestHandler that creates the bearer header on the http request message,  In the example below, `AddTokenHandler` is responsible for adding the bearer header to the http request prior to the send:
 
 ```cs
 // Create an http client for use by the DirectToEngine Client and add the token handler to the client.
 builder.Services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(
     () => new AddTokenHandler(settings));
 ```
 
 Then create an instance the client and request to start a conversation:
 
 ```cs
 var copilotClient = new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
 await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent:true, cancellationToken:cancellationToken))
 {
 
 }
 ```
 
 #### 2. Create a function that returns a user token and pass that to the constructor for the Copilot Client
 
 In this case, the `TokenService` is a class is responsible for managing the token acquire flow and returning the access token via the `GetToken API` call on the `TokenService` Class.
 > [!NOTE]
 > We do not cover how to create the TokenService Class in this document. The Copilot client is looking for a function that matches the signature `Func<string, Task<string>> tokenProviderFunction`
 
 ```cs
 var tokenService = new TokenService(settings);
 var copilotClient = new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), tokenService.GetToken, logger, "mcs");
 
 await foreach (Activity act in copilotClient.StartConversationAsync(emitStartConversationEvent:true, cancellationToken:cancellationToken))
 {
 
 }
 ```



