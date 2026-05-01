# A2AAgent Sample

This is a sample of a simple Agent that adds A2A support.  

> Note that this is a preview version of A2A support and is likely to change.

### Overview of A2A in Agents SDK
- SDK Agents can add support to an existing Agent in order to particapte in an A2A multi-agent scenario.
- Messages sent via A2A are handled in the same `AgentApplication` as other channels.  This allows the SDK developer to leverage existing functionality and stack knowledge.
- The `Microsoft.Agents.Hosting.AspNetCore.A2A` package enables support for A2A requests and response handling:
  - A2A `Task` state handling and persistence via `IStorage`.
  - SSE and polling

### Not supported in this version
- A2A
  - Push Notifications.  This will most likely be handled via `Adapter.ContinueConversation`
  - `tasks/resubscribe`.  This will be coming in order to fully support SSE.
  - Extensions.  These would be useful to support knowledge about Agents SDK payloads.  For example Streamed Responses, AI Citations, or Adaptive Cards responses.
  - We are not supplying an A2A Client.  Future support will come for this when Agents SDK adopts [a2aproject/a2a-dotnet](https://github.com/a2aproject/a2a-dotnet).
- SDK
  - Not implemented
    - `Adapter.ContinueConversation`
    - `Adapter.CreateConversation`
    - `Adapter.UpdateActivity`
    - `Adapter.DeleteActivity`
  - `Message` responses.  All interactions create an A2A `Task` (see details below)
  - `ITurnState.UserState` will not function as expected as we currently lack a unique userId for the A2A request.
- Multiple A2A agents in the same host are not supported.  All A2A request are routed to the registered `IAgent`.

## Prerequisites

- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0

## Running this sample

- This sample accepts anonymous requests out-of-the-box.
- To enable incoming Bearer token authentication 
  - Without Azure Bot Service support:
    - Create an App Registration in Azure using one of the SDK supported auth types.  Client Secret, User Managed Identity, and Federated Credentials are popular.
  - With Azure Bot Service support, create an Azure Bot with one of these authentication types
    - [SingleTenant, Client Secret](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-single-secret.md)
    - [SingleTenant, Federated Credentials](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-fic.md) 
    - [User Assigned Managed Identity](https://github.com/microsoft/Agents/blob/main/docs/HowTo/azurebot-create-msi.md)
  - Update appsettings "Connections.ServiceConnection" using: [Configure authentication in a .NET agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/microsoft-authentication-library-configuration-options).
  - Update appsettings "TokenValidation" to:
    ```json
    "TokenValidation": {
      "Enabled": true,
      "Audiences": [
        "{{ClientId}}"
      ],
      "TenantId": "{{TenantId}}"
    },
    ```
- By default, it will respond to A2A requests on `http://localhost:3978/a2a`

## Adding A2A support to an existing SDK Agent

1. Add a package dependency for `Microsoft.Agents.Hosting.AspNetCore.A2A`

1. Register the `A2AAdapter` in Program.cs
   
   ```csharp
   builder.Services.AddA2AAdapter();
   ```

1. Add the A2A endpoints in Program.cs

   ```csharp
   app.MapA2A(requireAuth: !app.Environment.IsDevelopment());
   ```

1. It is recommended that your `AgentApplication` implement `IAgentCardHandler`.  Not doing so will work fine for development purposes.  However, `AgentCard` properties like `Name`, `Description`, `Version`, and `Skills` will be defaulted.
   - Before `IAgentCardHandler.GetAgentCard` is called, the other properties on `AgentCard` have already been setup properly. See `MyAgent.cs` in this project.
   - The preview version of this will allow you change the other `AgentCard` values.  Do so at your A2A agents peril.  This will be restricted in the future as the SDK Hosting manages some of these values.

## Overview of A2A to Activity Protocol (and back)

1. An inbound A2A `Message` is converted to `Activity` and passed to your `AgentApplication`
   - `Message` -> `ActivityTypes.Message`
      - The `Activity.ChannelId` is "a2a" 
      - `TextPart` objects are appeneded to `Activity.Text`
      - Other `Part` types are  added as `Attachments`
      - `Activity.ChannelData` is the A2A `Task` instance (cast `Activity.ChannelData` to `AgentTask`).
   - `tasks/cancel` -> `ActivityTypes.EndOfConversation`
     - The A2A Task will be in a terminal state, so any `ITurnContext.Send*` calls will be ignored.
     - Do any needed conversation cleanup as you normally would when receiving `EndOfConversation`.  Delete ConversationState, etc...
     - Example handler for `EndOfConversation`
       ```csharp
       OnActivity(ActivityTypes.EndOfConversation, (turnContext, turnState, ct) =>
       {
          turnState.Conversation.ClearState();
          return Task.CompletedTask;
       }
       ```
   - You can use your `AgentApplication.OnMessage` route as you normally would.  If you need to handle A2A messages differently, you can use something like this to add a new message route in your `AgentApplication`:

      ```csharp
      OnMessage((turnContext, turnState, ct) =>
          {
              return Task.FromResult(turnContext.Activity.ChannelId == Channels.A2A);
          },
          OnA2AMessageAsync
      );
      ```
1. Outbound Activities sent via `ITurnContext.Send*`
    1. `ActivityTypes.Message` 
        1. `Activity.Text` -> `TextPart`
        1. `Attachments` -> `FilePart` for each
        1. `Activity.Entities` are included as `DataPart` for each, with schema in `DataPart.Metadata` property.
        1. `Activity.Value` -> `DataPart`  with schema in `Metadata`
        
    1. `ActivityTypes.EndOfConversation`
        1. `Activity.Value` retained as an `Artifact` with name of "Result" on the `AgentTask`
        1. Any `Activity.Text` is included in the `StatusUpdateEvent.Status.Message`
        1. The A2A Task will be in terminal state using these values in `Activity.Code` to set `Task.Status.State`:
           - `EndOfConversationCodes.Error` -> A2A `TaskState.Failed`
           - `EndOfConversationCodes.UserCancelled` -> A2A `TaskState.Canceled`
           - Anything else -> A2A `TaskState.Completed`

    1. StreamingResponses
        1. The streaming response results in an `Artifact` on the A2A `Task`.
        1. Use of `StreamingResponse.QueueInformative` sets `Task.Status.Message`
        1. The AI Citation Entity (if it exists) is included as a `DataPart` in the `Artifact`, but this is of limited value at the moment.
            
    1. Other Activity types are ignored

    1. Be explicit with setting `Activity.InputHint`.  This is required for A2A multi-turn behavior (see below).

## Turn concepts in A2A and Activity Protocol

### Single vs Multi turn
1. Activity Protocol
   1. Mutlti-turn by default using conversationId, but no enforced concept of "ended". It's a perpetual chat.
       1. The `Activity.Conversation.Id` is used to indicate which conversation it's for, and the agent uses this for state.
   1. However, `EndOfConversation` sent by either side signals "this conversation is complete".  When sent by the Agent, this Activity can contain a completion value in `Activity.Value`, and the result in `Activity.Code`.
   1. `EndOfConversation` is used in SDK when communicating with another agent (via Activity Protocol) to indicate that the conversation is over, with an optional result.
   1. While `EndOfConversation` signals completion, subsequent messages to a conversationId could still be acted on.  There isn't a notion of "complete".  If an SDK agent desired this, ConversationState would have to be used to track state, and reject further operations.

1. A2A
   1. A2A has two concepts of interaction
      1. Exchange of `Message` between client and server
      1. A2A `Task` (`AgentTask` in SDK)
      1. An interaction can start with just `Message` exchange, but can transition to a `Task`.
   1. Once a `Task` is created, no individual `Message` payload sent outside the task.  They would be sent via `Task.Status.Message`.
   1. Multi turn when `Task.Status.State == "input-required"`
   1. Until `Task.Status.State == "complete"`
   1. Once a `Task` is terminal (`TaskState.Complete`, `TaskState.Failed`, or `TaskState.Canceled`), it is immutable and can no longer be acted on.

### Current handling in `Microsoft.Agents.Hosting.A2A`
1. Everything is in the context of an A2A `Task`.  Agents SDK does not currently support the notion of a `Message` interaction that transitions to a `Task`.

1. SDK considers taskId as it's conversationId.  i.e., `ConversationState` keys on taskId (via `Activity.Conversation.Id`)
    1. This keeps state per-task even within the same A2A contextId.
    1. While SDK maintains contextId per A2A expectations, this isn't currently utilized at the `AgentApplication` level.  However, an SDK agent could access full `Task` information via `Activity.ChannelData`.

1. The SDK concept of "end of conversation" is quite similar to "Task is complete"

1. SDK responses of `Activity.InputHint == InputHints.ExpectingInput` will result in A2A `Task.Status.State.InputRequired` 
   1. Ideally this should be the last Activity sent in a turn.

1. The SDK Agent must explicitly send `ActivityTypes.EndOfConversation` to complete a `Task`:
    1. `Activity.Code` sets `Task.Status.State`
        1. `EndOfConversationCodes.Error` => `TaskState.Failed`
        1. `EndOfConversationCodes.UserCancelled` => `TaskState.Canceled`
        1. _ =>  `TaskState.Completed`
    1. `Activity.Value` added as `Artifact`
    1. `Activity.Text` sets the `Task.Status.Message`
    1. `EndOfConversation` should be the last Activity sent.  Subsequent `ITurnContext.Send*` will be ignored as the `Task` will be terminal.

1. This may not be the final handling of this
    1. This means agents like EchoAgent will never complete.  It never emits an `EndOfConversation`.  This is a "chat bot" without end.  The task being... "chat forever".
    1. Agent dev will need to be more aware of properties like `Activity.InputHint` and sending EOC.
    1. Knowing when a task is complete can be a challenge.

## Interacting with this sample
> The Python [A2A CLI](https://github.com/a2aproject/a2a-samples/tree/main/samples/python/hosts/cli) is a useful tool for exercising this sample.  Other A2A Clients have not been tested.

- Sending *"-multi"* demonstrates a multi-turn interaction.  Send *"end"* to complete the task.
- Sending *"-stream"* demonstrates `ITurnContext.StreamingResponse`
- Any other input is echoed back.
  - This is different in details from the `EmptyAgent` sample in that since everything is an A2A `Task`, we send each echo response as an `EndOfConversation`.

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/) repo.