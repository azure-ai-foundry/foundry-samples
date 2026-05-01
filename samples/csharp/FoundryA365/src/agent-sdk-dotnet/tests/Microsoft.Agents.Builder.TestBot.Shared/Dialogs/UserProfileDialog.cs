// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.Dialogs.Choices;
using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.TestBot.Shared.Dialogs
{
    public class UserProfileDialog : ComponentDialog
    {
        private UserState _userState;

        public UserProfileDialog(UserState userState)
            : base("root")
        {
            _userState = userState;

            // This array defines how the Waterfall will execute.
            var waterfallActions = new WaterfallStep[]
            {
                TransportActionAsync,
                NameActionAsync,
                NameConfirmActionAsync,
                AgeActionAsync,
                ConfirmActionAsync,
                SummaryActionAsync,
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallActions));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>), AgePromptValidatorAsync));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private static async Task<DialogTurnResult> TransportActionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            // Running a prompt here means the next WaterfallStep will be run when the users response is received.
            return await stepContext.PromptAsync(
                nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please enter your mode of transport."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Car", "Bus", "Bicycle" }),
                }, cancellationToken);
        }

        private static async Task<DialogTurnResult> NameActionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["transport"] = ((FoundChoice)stepContext.Result).Value;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Please enter your name.") }, cancellationToken);
        }

        private static Task<bool> AgePromptValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            // This condition is our validation rule. You can also change the value at this point.
            return Task.FromResult(promptContext.Recognized.Value >= 0 && promptContext.Recognized.Value < 150);
        }

        private async Task<DialogTurnResult> NameConfirmActionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["name"] = (string)stepContext.Result;

            // We can send messages to the user at any point in the WaterfallStep.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Thanks {stepContext.Result}."), cancellationToken);

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog; here it is a Prompt Dialog.
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Would you like to give your age?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> AgeActionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                // User said "yes" so we will be prompting for the age.
                // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
                var promptOptions = new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please enter your age."),
                    RetryPrompt = MessageFactory.Text("The value entered must be greater than 0 and less than 150."),
                };

                return await stepContext.PromptAsync(nameof(NumberPrompt<int>), promptOptions, cancellationToken);
            }
            else
            {
                // User said "no" so we will skip the next step. Give -1 as the age.
                return await stepContext.NextAsync(-1, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ConfirmActionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["age"] = (int)stepContext.Result;

            // We can send messages to the user at any point in the WaterfallStep.
            // var msg = userProfile.Age == -1 ? "No age given." : $"I have your age as {userProfile.Age}.";
            var msg = (int)stepContext.Values["age"] == -1 ? "No age given." : $"I have your age as {stepContext.Values["age"]}.";

            // We can send messages to the user at any point in the WaterfallStep.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is a Prompt Dialog.
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Is this ok?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> SummaryActionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                // Get the current profile object from user state.
                var userProfile = _userState.GetValue<UserProfile>("UserProfile", () => new UserProfile());

                userProfile.Transport = (string)stepContext.Values["transport"];
                userProfile.Name = (string)stepContext.Values["name"];
                userProfile.Age = (int)stepContext.Values["age"];

                var msg = $"I have your mode of transport as {userProfile.Transport} and your name as {userProfile.Name}.";
                if (userProfile.Age != -1)
                {
                    msg += $" And age as {userProfile.Age}.";
                }

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thanks. Your profile will not be kept."), cancellationToken);
            }

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
