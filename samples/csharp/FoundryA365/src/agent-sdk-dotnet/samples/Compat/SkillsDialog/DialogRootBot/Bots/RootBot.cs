// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DialogRootBot.Bots
{
    public class RootBot<T> : ActivityHandler
        where T : Dialog
    {
        private readonly ConversationState _conversationState;
        private readonly Dialog _mainDialog;

        public RootBot(ConversationState conversationState, T mainDialog)
        {
            _conversationState = conversationState;
            _mainDialog = mainDialog;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.Type != ActivityTypes.ConversationUpdate)
            {
                // Run the Dialog with the Activity.
#pragma warning disable CS0618 // Type or member is obsolete
                await _mainDialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else
            {
                // Let the base class handle the activity.
                await base.OnTurnAsync(turnContext, cancellationToken);
            }

            // Save any state changes that might have occurred during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                // Greet anyone that was not the target (recipient) of this message.
                // To learn more about Adaptive Cards, see https://aka.ms/msbot-adaptivecards.
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var welcomeCard = CreateAdaptiveCardAttachment();
                    var activity = MessageFactory.Attachment(welcomeCard);
                    activity.Speak = "Welcome to the Dialog Skill Prototype!";
                    await turnContext.SendActivityAsync(activity, cancellationToken);
#pragma warning disable CS0618 // Type or member is obsolete
                    await _mainDialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
                }
            }
        }

        // Load attachment from embedded resource.
        private Attachment CreateAdaptiveCardAttachment()
        {
            var cardResourcePath = "DialogRootBot.Cards.welcomeCard.json";

            using (var stream = GetType().Assembly.GetManifestResourceStream(cardResourcePath))
            {
                using (var reader = new StreamReader(stream))
                {
                    var adaptiveCard = reader.ReadToEnd();
                    return new Attachment
                    {
                        ContentType = ContentTypes.AdaptiveCard,
                        Content = adaptiveCard
                    };
                }
            }
        }
    }
}
