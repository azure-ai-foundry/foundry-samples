// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Builder.TestBot.Shared.Bots
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T> : ActivityHandler
        where T : Dialog
    {
        public DialogBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
        }

        protected Dialog Dialog { get; }

        protected AgentState ConversationState { get; }

        protected AgentState UserState { get; }

        protected ILogger Logger { get; }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await ConversationState.LoadAsync(turnContext, false, cancellationToken);
            await UserState.LoadAsync(turnContext, false, cancellationToken);

            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");

            // Run the Dialog with the new message Activity.
            await Dialog.Run(turnContext, ConversationState, cancellationToken);
        }
    }
}
