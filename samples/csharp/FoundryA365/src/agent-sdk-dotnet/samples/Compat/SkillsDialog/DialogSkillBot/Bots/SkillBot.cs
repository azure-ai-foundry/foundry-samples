// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.State;
using System.Threading;
using System.Threading.Tasks;

namespace DialogSkillBot.Bots
{
    public class SkillBot<T> : ActivityHandler
        where T : Dialog
    {
        private readonly ConversationState _conversationState;
        private readonly Dialog _mainDialog;

        public SkillBot(ConversationState conversationState, T mainDialog)
        {
            _conversationState = conversationState;
            _mainDialog = mainDialog;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            await _mainDialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

            // Save any state changes that might have occurred during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }
    }
}
