// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DialogSkillBot.Dialogs
{
    public class DateResolverDialog : CancelAndHelpDialog
    {
        private const string PromptMsgText = "When would you like to travel?";
        private const string RepromptMsgText = "I'm sorry, to make your booking please enter a full travel date, including Day, Month, and Year.";

        public DateResolverDialog(string id = null)
            : base(id ?? nameof(DateResolverDialog))
        {
            AddDialog(new DateTimePrompt(nameof(DateTimePrompt), DateTimePromptValidator));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[] { InitialStepAsync, FinalStepAsync }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private static Task<bool> DateTimePromptValidator(PromptValidatorContext<IList<DateTimeResolution>> promptContext, CancellationToken cancellationToken)
        {
            /*
            if (promptContext.Recognized.Succeeded)
            {
                // This value will be a TIMEX. We are only interested in the Date part, so grab the first result and drop the Time part.
                // TIMEX is a format that represents DateTime expressions that include some ambiguity, such as a missing Year.
                var timex = promptContext.Recognized.Value[0].Timex.Split('T')[0];

                // If this is a definite Date that includes year, month and day we are good; otherwise, reprompt.
                // A better solution might be to let the user know what part is actually missing.
                var isDefinite = new TimexProperty(timex).Types.Contains(Constants.TimexTypes.Definite);

                return Task.FromResult(isDefinite);
            }
            */

            return Task.FromResult(true);
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = (string)stepContext.Options;

            var promptMessage = MessageFactory.Text(PromptMsgText, PromptMsgText, InputHints.ExpectingInput);
            var repromptMessage = MessageFactory.Text(RepromptMsgText, RepromptMsgText, InputHints.ExpectingInput);

            if (timex == null)
            {
                // We were not given any date at all so prompt the user.
                return await stepContext.PromptAsync(
                    nameof(DateTimePrompt),
                    new PromptOptions
                    {
                        Prompt = promptMessage,
                        RetryPrompt = repromptMessage,
                    }, cancellationToken);
            }

            // Regex: \d{1,2}\/\d{1,2}\/\d{2,4}
            // We have a Date we just need to check it is unambiguous.

            /*
            var timexProperty = new TimexProperty(timex);
            if (!timexProperty.Types.Contains(Constants.TimexTypes.Definite))
            {
                // This is essentially a "reprompt" of the data we were given up front.
                return await stepContext.PromptAsync(
                    nameof(DateTimePrompt),
                    new PromptOptions
                    {
                        Prompt = repromptMessage,
                    }, cancellationToken);
            }
            */

            return await stepContext.NextAsync(new List<DateTimeResolution> { new DateTimeResolution { Timex = timex } }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var timex = ((List<DateTimeResolution>)stepContext.Result)[0].Timex;
            return await stepContext.EndDialogAsync(timex, cancellationToken);
        }
    }
}
