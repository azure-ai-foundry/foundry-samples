# Orchestrated Client Console Sample

This sample demonstrates externally orchestrated conversations with a Copilot Studio agent using the `OrchestratedClient`. Unlike the standard `CopilotClient` which manages conversation flow internally, the `OrchestratedClient` gives the caller full control over the conversation lifecycle — starting sessions, forwarding user input, invoking tools, and sending conversation updates.

## Instructions - Setup

### Prerequisites

To run this sample, you will need the following:

1. An Agent created in Microsoft Copilot Studio
1. Ability to create an Application Identity in Azure for a Public Client/Native App Registration, or access to an existing one with the `CopilotStudio.Copilots.Invoke` API permission assigned.

### Create an Agent in Copilot Studio

1. Create an Agent in [Copilot Studio](https://copilotstudio.microsoft.com)
    1. Publish your newly created Agent
    1. In Copilot Studio, go to Settings => Advanced => Metadata and copy the following values (you will need them later):
        1. Schema name
        1. Environment Id
        1. Bot Id (CdsBotId)

### Create an Application Registration in Entra ID

This step requires permissions to create application identities in your Azure tenant. For this sample, you will be creating a Native Client Application Identity (no secrets required).

1. Open https://portal.azure.com
1. Navigate to Entra ID
1. Create a new App Registration in Entra ID
    1. Provide a Name
    1. Choose "Accounts in this organization directory only"
    1. In the "Select a Platform" list, choose "Public Client/native (mobile & desktop)"
    1. In the Redirect URI url box, type in `http://localhost` (**note: use HTTP, not HTTPS**)
    1. Then click Register.
1. In your newly created application:
    1. On the Overview page, note down for later:
        1. The Application (client) ID
        1. The Directory (tenant) ID
    1. Go to Manage => API Permissions
    1. Click Add Permission
        1. In the side panel, click the tab `APIs my organization uses`
        1. Search for `Power Platform API`
            1. *If you do not see `Power Platform API`, see the tip below.*
        1. In the permissions list, choose `Delegated Permissions`, then `CopilotStudio`, and check `CopilotStudio.Copilots.Invoke`
        1. Click `Add Permissions`
    1. (Optional) Click `Grant Admin consent for copilotsdk`
    1. On the Authentication page, under `Advanced settings`, make sure the `Enable the following mobile and desktop flows` toggle is set to `Yes`.
    1. Close Azure Portal

> [!TIP]
> If you do not see `Power Platform API` in the list of APIs your organization uses, you need to add the Power Platform API to your tenant. Go to [Power Platform API Authentication](https://learn.microsoft.com/power-platform/admin/programmability-authentication-v2#step-2-configure-api-permissions) and follow the instructions on Step 2 to add the Power Platform Admin API to your tenant.

### Configure the Sample

1. Open `appsettings.json` in the `OrchestratedClient` project.
1. Fill in the placeholder values using the information recorded during setup:

```json
"CopilotStudioClientSettings": {
    "EnvironmentId": "",   // Environment ID of the environment with the Copilot Studio Agent
    "SchemaName": "",      // Schema Name of the Copilot to use
    "CdsBotId": "",        // Bot ID from Copilot Studio metadata (required for orchestrated connections)
    "TenantId": "",        // Tenant ID of the App Registration (same tenant as the Copilot)
    "AppClientId": ""      // App ID of the App Registration
}
```

> [!NOTE]
> You can alternatively use `DirectConnectUrl` instead of `EnvironmentId` and `SchemaName` if you have a direct connection URL for your agent.

## Running the Sample

1. Build and run the project:

```bash
dotnet run --project src/samples/CopilotStudioClient/OrchestratedClient
```

2. The app will challenge you for login in a new browser window or tab. After authentication, it will start an orchestrated conversation with the Copilot Studio agent.

3. The initial `StartConversation` request is sent automatically. After the agent responds, you can send subsequent requests as raw JSON `OrchestratedTurnRequest` objects.

## Usage

After the initial conversation starts, the console prompts you with `request>` where you paste JSON requests. The supported operations are:

### StartConversation
Starts a new conversation session:
```json
{"orchestration":{"operation":"StartConversation"}}
```

### HandleUserResponse
Sends a user message to the agent:
```json
{"orchestration":{"operation":"HandleUserResponse"},"activity":{"type":"message","text":"hello"}}
```

### InvokeTool
Invokes a tool on behalf of the agent:
```json
{"orchestration":{"operation":"InvokeTool","toolInputs":{"toolSchemaName":"myTool"}},"activity":{"type":"message","text":"tool result"}}
```

### ConversationUpdate
Sends a conversation update event:
```json
{"orchestration":{"operation":"ConversationUpdate"}}
```

## Authentication

The OrchestratedClient requires a token provided by the developer to operate. This sample uses a user-interactive flow (MSAL) to obtain the token for the application ID created above.

The client uses a named `HttpClient` retrieved from `IHttpClientFactory` (named `orchestrated`) injected via DI. This client is configured with a `DelegatingHandler` (`AddTokenHandler`) to apply a valid Entra ID token using MSAL.

## Architecture

| File | Description |
|------|-------------|
| `Program.cs` | DI setup with `IHttpClientFactory`, `OrchestratedClient` registration, and hosted service |
| `OrchestratedChatConsoleService.cs` | Interactive console that starts an orchestrated conversation then accepts raw JSON input |
| `AddTokenHandler.cs` | MSAL interactive authentication handler with token caching |
| `SampleConnectionSettings.cs` | Extended connection settings with auth properties (TenantId, AppClientId) |
| `TimeSpanExtensions.cs` | Duration formatting helper for diagnostics |
