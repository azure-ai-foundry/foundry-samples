// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HandlingAttachments;

public class AttachmentsAgent : AgentApplication
{
    public AttachmentsAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    $"Welcome to HandlingAttachment Agent." +
                    $" This bot will introduce you to Attachments." +
                    $" Please select an option",
                    cancellationToken: cancellationToken);
                await DisplayOptionsAsync(turnContext, cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var reply = await ProcessInput(turnContext, turnState, cancellationToken);
        if (reply != null)
        {
            // Respond to the user.
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }
        await DisplayOptionsAsync(turnContext, cancellationToken);
    }

    private static async Task DisplayOptionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
    {
        // Create a HeroCard with options for the user to interact with the bot.
        var card = new HeroCard
        {
            Text = "You can upload an image or select one of the following choices",
            Buttons =
            [
                // Note that some channels require different values to be used in order to get buttons to display text.
                // In this code the emulator is accounted for with the 'title' parameter, but in other channels you may
                // need to provide a value for other parameters like 'text' or 'displayText'.
                new CardAction(ActionTypes.ImBack, title: "1. Inline Attachment", value: "1"),
                new CardAction(ActionTypes.ImBack, title: "2. Internet Attachment", value: "2"),
            ],
        };

        if (turnContext.Activity.ChannelId == Channels.Msteams)
        {
            card.Buttons.Add(new CardAction(ActionTypes.ImBack, title: "3. Uploaded Attachment", value: "3"));
        }

        var reply = MessageFactory.Attachment(card.ToAttachment());
        await turnContext.SendActivityAsync(reply, cancellationToken);
    }

    // Given the input from the message, create the response.
    private static async Task<IActivity?> ProcessInput(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        IActivity? reply;

        if (turnState.Temp.InputFiles.Any())
        {
            reply = MessageFactory.Text($"There are {turnState.Temp.InputFiles.Count} attachments.");

            var imageData = Convert.ToBase64String(turnState.Temp.InputFiles[0].Content);
            reply.Attachments = [new Attachment() { Name = turnState.Temp.InputFiles[0].Filename, ContentType = "image/png", ContentUrl = $"data:image/png;base64,{imageData}" }];
        }
        else
        {
            // Send at attachment to the user.
            reply = await HandleOutgoingAttachment(turnContext, turnContext.Activity, cancellationToken);
        }

        return reply;
    }

    // Returns a reply with the requested Attachment
    private static async Task<IActivity?> HandleOutgoingAttachment(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
    {
        // Look at the user input, and figure out what kind of attachment to send.

        if (string.IsNullOrEmpty(activity.Text))
        {
            return null;
        }

        IActivity? reply = null;

        if (activity.Text.StartsWith('1'))
        {
            reply = MessageFactory.Text("This is an inline attachment.");
            reply.Attachments = [GetInlineAttachment()];
        }
        else if (activity.Text.StartsWith('2'))
        {
            reply = MessageFactory.Text("This is an attachment from a HTTP URL.");
            reply.Attachments = [GetInternetAttachment()];
        }
        else if (activity.Text.StartsWith('3'))
        {
            reply = MessageFactory.Text("This is an uploaded attachment.");

            // Get the uploaded attachment.
            var uploadedAttachment = await UploadAttachmentAsync(turnContext, activity.ServiceUrl, activity.Conversation.Id, cancellationToken);
            reply.Attachments = [uploadedAttachment];
        }

        return reply;
    }

    // Creates an inline attachment sent from the bot to the user using a base64 string.
    // Using a base64 string to send an attachment will not work on all channels.
    // Additionally, some channels will only allow certain file types to be sent this way.
    // For example a .png file may work but a .pdf file may not on some channels.
    // Please consult the channel documentation for specifics.
    private static Attachment GetInlineAttachment()
    {
        var imagePath = Path.Combine(Environment.CurrentDirectory, @"Resources", "build-agents.png");
        var imageData = Convert.ToBase64String(File.ReadAllBytes(imagePath));

        return new Attachment
        {
            Name = @"Resources\build-agents.png",
            ContentType = "image/png",
            ContentUrl = $"data:image/png;base64,{imageData}",
        };
    }

    // Creates an "Attachment" to be sent from the bot to the user from an uploaded file.
    private static async Task<Attachment> UploadAttachmentAsync(ITurnContext turnContext, string serviceUrl, string conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            throw new ArgumentNullException(nameof(serviceUrl));
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentNullException(nameof(conversationId));
        }

        var imagePath = Path.Combine(Environment.CurrentDirectory, @"Resources", "agents-sdk.png");

        var connector = turnContext.Services.Get<IConnectorClient>();

        // This only supports payloads smaller than 260k
        var response = await connector.Conversations.UploadAttachmentAsync(
            conversationId,
            new AttachmentData
            {
                Name = @"Resources\agents-sdk.png",
                OriginalBase64 = File.ReadAllBytes(imagePath),
                Type = "image/png",
            },
            cancellationToken);

        var attachmentUri = connector.Attachments.GetAttachmentUri(response.Id);

        return new Attachment
        {
            Name = @"Resources\agents-sdk.png",
            ContentType = "image/png",
            ContentUrl = attachmentUri,
        };
    }

    // Creates an <see cref="Attachment"/> to be sent from the bot to the user from a HTTP URL.
    private static Attachment GetInternetAttachment()
    {
        // ContentUrl must be HTTPS.
        return new Attachment
        {
            Name = @"Resources\introducing-agents-sdk.png",
            ContentType = "image/png",
            ContentUrl = "https://devblogs.microsoft.com/microsoft365dev/wp-content/uploads/sites/73/2024/11/word-image-23435-1.png",
        };
    }
}
