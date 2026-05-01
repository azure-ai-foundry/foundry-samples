// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs;
using System;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Builder.TestBot.Shared
{
    public static class DialogExtensions
    {
        public static async Task Run(this Dialog dialog, ITurnContext turnContext, AgentState state, CancellationToken cancellationToken)
        {
            var dialogState = state.GetValue<DialogState>("DialogState", () => new DialogState());
            var dialogSet = new DialogSet(dialogState);
            dialogSet.Add(dialog);

            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);
            var results = await dialogContext.ContinueDialogAsync(cancellationToken);
            if (results.Status == DialogTurnStatus.Empty)
            {
                await dialogContext.BeginDialogAsync(dialog.Id, null, cancellationToken);
            }
        }

        [Obsolete("Use DialogExtensions.Run(Dialog, ITurnContext, BotState, CancellationToken)")]
        public static async Task RunAsync(this Dialog dialog, ITurnContext turnContext, IStatePropertyAccessor<DialogState> accessor, CancellationToken cancellationToken)
        {
            var dialogSet = new DialogSet(accessor);
            dialogSet.Add(dialog);

            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);
            var results = await dialogContext.ContinueDialogAsync(cancellationToken);
            if (results.Status == DialogTurnStatus.Empty)
            {
                await dialogContext.BeginDialogAsync(dialog.Id, null, cancellationToken);
            }
        }
    }
}
