// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Client;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Client.Compat;

namespace Microsoft.Agents.Builder.Dialogs
{
    /// <summary>
    /// Provides extension methods for <see cref="Dialog"/> and derived classes.
    /// </summary>
    public static class DialogExtensions
    {
        // Back compat
        private const string ToChannelFromBotOAuthScope = "https://api.botframework.com";
        private const string GovToChannelFromBotOAuthScope = "https://api.botframework.us";

        /// <summary>
        /// Creates a dialog stack and starts a dialog, pushing it onto the stack.
        /// </summary>
        /// <param name="dialog">The dialog to start.</param>
        /// <param name="turnContext">The context for the current turn of the conversation.</param>
        /// <param name="state">AgentState to use for state.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task<DialogTurnResult> RunAsync(this Dialog dialog, ITurnContext turnContext, AgentState state, CancellationToken cancellationToken)
        {
            var dialogState = state.GetValue<DialogState>("DialogState", () => new DialogState());
            var dialogSet = new DialogSet(dialogState);

            dialogSet.Add(dialog);

            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken).ConfigureAwait(false);

            return await InternalRunAsync(turnContext, dialog.Id, dialogContext, cancellationToken).ConfigureAwait(false);
        }

        [Obsolete("Use the non-IStatePropertyAccessor version")]
        public static async Task RunAsync(this Dialog dialog, ITurnContext turnContext, IStatePropertyAccessor<DialogState> accessor, CancellationToken cancellationToken)
        {
            var state = await accessor.GetAsync(turnContext, () => { return new DialogState(); }, cancellationToken).ConfigureAwait(false);

            var dialogSet = new DialogSet(state);

            dialogSet.Add(dialog);

            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken).ConfigureAwait(false);

            await InternalRunAsync(turnContext, dialog.Id, dialogContext, cancellationToken).ConfigureAwait(false);
        }


        internal static async Task<DialogTurnResult> InternalRunAsync(ITurnContext turnContext, string dialogId, DialogContext dialogContext, CancellationToken cancellationToken)
        {
            DialogTurnResult dialogTurnResult = null;

            // Loop as long as we are getting valid OnError handled we should continue executing the actions for the turn.
            //
            // NOTE: We loop around this block because each pass through we either complete the turn and break out of the loop
            // or we have had an exception AND there was an OnError action which captured the error.  We need to continue the 
            // turn based on the actions the OnError handler introduced.
            var endOfTurn = false;
            while (!endOfTurn)
            {
                try
                {
                    dialogTurnResult = await InnerRunAsync(turnContext, dialogId, dialogContext, cancellationToken).ConfigureAwait(false);

                    // turn successfully completed, break the loop
                    endOfTurn = true;
                }
                catch (Exception err)
                {
                    var handled = false;
                    var innerExceptions = new List<Exception>();
                    try
                    {
                        // fire error event, bubbling from the leaf.
                        handled = await dialogContext.EmitEventAsync(DialogEvents.Error, err, bubble: true, fromLeaf: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception emitErr)
                    {
                        innerExceptions.Add(emitErr);
                    }

                    if (innerExceptions.Count > 0)
                    {
                        innerExceptions.Add(err);
                        throw new AggregateException("Unable to emit the error as a DialogEvent.", innerExceptions);
                    }

                    if (!handled)
                    {
                        // error was NOT handled, throw the exception and end the turn. (This will trigger the Adapter.OnError handler and end the entire dialog stack)
                        throw;
                    }
                }
            }

            // return the redundant result because the DialogManager contract expects it
            return dialogTurnResult;
        }

        private static async Task<DialogTurnResult> InnerRunAsync(ITurnContext turnContext, string dialogId, DialogContext dialogContext, CancellationToken cancellationToken)
        {
            // Handle EoC and Reprompt event from a parent Agent (can be root bot to skill or skill to skill)
            if (IsFromParentToSkill(turnContext))
            {
                // Handle remote cancellation request from parent.
                if (turnContext.Activity.Type == ActivityTypes.EndOfConversation)
                {
                    if (dialogContext.Stack.Count == 0)
                    {
                        // No dialogs to cancel, just return.
                        return new DialogTurnResult(DialogTurnStatus.Empty);
                    }

                    var activeDialogContext = GetActiveDialogContext(dialogContext);

                    // Send cancellation message to the top dialog in the stack to ensure all the parents are canceled in the right order. 
                    return await activeDialogContext.CancelAllDialogsAsync(true, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                // Handle a reprompt event sent from the parent.
                if (turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == DialogEvents.RepromptDialog)
                {
                    if (dialogContext.Stack.Count == 0)
                    {
                        // No dialogs to reprompt, just return.
                        return new DialogTurnResult(DialogTurnStatus.Empty);
                    }

                    await dialogContext.RepromptDialogAsync(cancellationToken).ConfigureAwait(false);
                    return new DialogTurnResult(DialogTurnStatus.Waiting);
                }
            }

            // Continue or start the dialog.
            var result = await dialogContext.ContinueDialogAsync(cancellationToken).ConfigureAwait(false);
            if (result.Status == DialogTurnStatus.Empty)
            {
                result = await dialogContext.BeginDialogAsync(dialogId, null, cancellationToken).ConfigureAwait(false);
            }

            // Skills should send EoC when the dialog completes.
            if (result.Status == DialogTurnStatus.Complete || result.Status == DialogTurnStatus.Cancelled)
            {
                if (SendEoCToParent(turnContext))
                {
                    // Send End of conversation at the end.
                    var code = result.Status == DialogTurnStatus.Complete ? EndOfConversationCodes.CompletedSuccessfully : EndOfConversationCodes.UserCancelled;
                    var activity = new Activity(ActivityTypes.EndOfConversation) { Value = result.Result, Locale = turnContext.Activity.Locale, Code = code };
                    await turnContext.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
                }
            }

            return result;
        }

        /// <summary>
        /// Helper to determine if we should send an EoC to the parent or not.
        /// </summary>
        private static bool SendEoCToParent(ITurnContext turnContext)
        {
            if (turnContext.Identity as ClaimsIdentity != null && (AgentClaims.AllowAnonymous(turnContext.Identity) || AgentClaims.IsAgentClaim(turnContext.Identity)))
            {
                // EoC Activities returned by skills are bounced back to the Agent by SkillHandler.
                // In those cases we will have a SkillConversationReference instance in state.
                var skillConversationReference = turnContext.StackState.Get<ChannelConversationReference>(SkillChannelApiHandler.SkillConversationReferenceKey);
                if (skillConversationReference != null)
                {
                    // If the skillConversationReference.OAuthScope is for one of the supported channels, we are at the root and we should not send an EoC.
                    return skillConversationReference.OAuthScope != ToChannelFromBotOAuthScope && skillConversationReference.OAuthScope != GovToChannelFromBotOAuthScope;
                }

                return true;
            }

            return false;
        }

        private static bool IsFromParentToSkill(ITurnContext turnContext)
        {
            if (turnContext.StackState.Get<ChannelConversationReference>(SkillChannelApiHandler.SkillConversationReferenceKey) != null)
            {
                return false;
            }

            return turnContext.Identity as ClaimsIdentity != null && AgentClaims.IsAgentClaim(turnContext.Identity);
        }

        // Recursively walk up the DC stack to find the active DC.
        private static DialogContext GetActiveDialogContext(DialogContext dialogContext)
        {
            var child = dialogContext.Child;
            if (child == null)
            {
                return dialogContext;
            }

            return GetActiveDialogContext(child);
        }
    }
}
