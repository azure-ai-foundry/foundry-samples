# SkillDialog

**This is a migration of the Bot Framework DotNet SDK sample `81.skills-skilldialog` to Agents SDK.**

This bot has been created using the [Agents SDK](https://github.com/microsoft/agents-for-net).

## Key concepts in this sample

The solution uses dialogs, within both a parent bot (`DialogRootBot`) and a skill bot (`DialogSkillBot`).
It demonstrates how to post activities from the parent bot to the skill bot and return the skill responses to the user.

- `DialogRootBot`: this project shows how to consume a skill bot using a `SkillDialog`. 
- `DialogSkillBot`: this project shows a skill bot responding to messages sent by `DialogRootBot`.

## Migration Overview
- The sample from Bot Framework could not be migrated in its entirety since Agents SDK does not have LUIS support, and that was a major element of this sample.
- The migrated sample demonstrates the continued use of:
 - ActivityHandler
 - Dialogs
 - ConversationState
   - And use of obsoleted `IStatePropertAccessor`
 - Skills and SkillDialog

## Migration Detail

- These are notes specific to migrating a SkillDialog sample.  Follow the more general guidelines for migrating other aspects of a Bot Framework SDK bot, including steps around startup, configuration, authentication setup, and the more general changes that need to happen.
- This does not cover `DialogSkillBot` as this will be the same guidelines as for any non-Skills bot.

1. appsettings
   1. Orignal
      ```json
      "SkillHostEndpoint": "http://localhost:3978/api/skills/",
      "BotFrameworkSkills": [{
        "Id": "DialogSkillBot",
        "AppId": "",
        "SkillEndpoint": "http://localhost:39783/api/messages"
      }]
      ```
   2. New
      ```json
      "Agent": {
        "ClientId": "{{DialogRootBotClientId}}",
        "Host": {
          "DefaultResponseEndpoint": "http://localhost:3978/api/agentresponse/",
          "Agents": {
            "DialogSkillBot": {
              "ConnectionSettings": {
                "ClientId": "{{DialogSkillBotClientId}}",
                "Endpoint": "http://localhost:39783/api/messages",
                "TokenProvider": "BotServiceConnection"
              }
            }
          }
        }
      }
      ```
2. Startup
   1. Would have included these lines related to Skills DI
      ```csharp
      // Register the skills conversation ID factory, the client and the request handler.
      services.AddSingleton<SkillConversationIdFactoryBase, SkillConversationIdFactory>();
      services.AddSingleton<ChannelServiceHandlerBase, CloudSkillHandler>();
      ```
   2. Replaced with
      ```csharp
      // Add ChannelHost to enable calling other Agents.  This is also required for
      // AgentApplication.ChannelResponses use.
      builder.AddAgentHost<SkillChannelApiHandler>();
      ``` 
3. `MainDialog`
   1. The change in Agents SDK is that the separate entities of `SkillsConfiguration` and `SkillConversationIdFactoryBase` in Bot Framework have been combined into `IAgentHost`.
   1. Most of the changes are related to using the new interface and `SkillDialogOptions`.
   1. Recommended to diff the original and new `MainDialog.cs` files to get a better understanding of the types of changes.
    

## Further reading
To learn more about building Agents, see our [Microsoft 365 Agents SDK](https://github.com/microsoft/agents) repo.
