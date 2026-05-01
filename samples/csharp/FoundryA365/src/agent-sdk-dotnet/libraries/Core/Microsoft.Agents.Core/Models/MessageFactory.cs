// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Contains utility methods for various message types an Agent can return.
    /// </summary>
    /// <example>
    /// <code>
    /// // Create and send a message.
    /// var message = MessageFactory.Text("Hello World");
    /// await context.SendActivity(message);
    /// </code>
    /// </example>
    /// <remarks>The following apply to message actions in general.
    /// <para>See the channel's documentation for limits imposed upon the contents of
    /// the text of the message to send.</para>
    /// <para>To control various characteristics of your Agent's speech such as voice,
    /// rate, volume, pronunciation, and pitch, specify text to speak in
    /// Speech Synthesis Markup Language (SSML) format.</para>
    /// <para>
    /// Channels decide how each card action manifests in their user experience.
    /// In most cases, the cards are clickable. In others, they may be selected by speech
    /// input. In cases where the channel does not offer an interactive activation
    /// experience (e.g., when interacting over SMS), the channel may not support
    /// activation whatsoever. The decision about how to render actions is controlled by
    /// normative requirements elsewhere in this document (e.g. within the card format,
    /// or within the suggested actions definition).</para>
    /// </remarks>
    public static class MessageFactory
    {
        /// <summary>
        /// Returns a simple text message.
        /// </summary>
        /// <example>
        /// <code>
        /// // Create and send a message.
        /// var message = MessageFactory.Text("Hello World");
        /// await context.SendActivity(message);
        /// </code>
        /// </example>
        /// <param name="text">The text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <returns>A message activity containing the text.</returns>
        public static IActivity Text(string text, string ssml = null, string inputHint = null)
        {
            var ma = CreateMessageActivity();
            SetTextAndSpeak(ma, text, ssml, inputHint);
            return ma;
        }

        /// <summary>
        /// Returns a message that includes a set of suggested actions and optional text.
        /// </summary>
        /// <example>
        /// <code>
        /// // Create the activity and add suggested actions.
        /// var activity = MessageFactory.SuggestedActions(
        ///     new string[] { "red", "green", "blue" },
        ///     text: "Choose a color");
        ///
        /// // Send the activity as a reply to the user.
        /// await context.SendActivity(activity);
        /// </code>
        /// </example>
        /// <param name="actions">
        /// The text of the actions to create.
        /// </param>
        /// <param name="text">The text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <returns>A message activity containing the suggested actions.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="actions"/> is <c>null</c>.</exception>
        /// <remarks>This method creates a suggested action for each string in <paramref name="actions"/>.
        /// The created action uses the text for the <see cref="Microsoft.Agents.Core.Models.CardAction.Value"/> and
        /// <see cref="Microsoft.Agents.Core.Models.CardAction.Title"/> and sets the <see cref="Microsoft.Agents.Core.Models.CardAction.Type"/> to
        /// <see cref="Microsoft.Agents.Core.Models.ActionTypes.ImBack"/>.
        /// </remarks>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.SuggestedActions(System.Collections.Generic.IEnumerable{Microsoft.Agents.Core.Models.CardAction}, string, string, string)"/>
        public static IActivity SuggestedActions(IEnumerable<string> actions, string text = null, string ssml = null, string inputHint = null)
        {
            return SuggestedActions(actions, text, ssml, inputHint, null);
        }

        /// <summary>
        /// Returns a message that includes a set of suggested actions and optional text.
        /// </summary>
        /// <example>
        /// <code>
        /// // Create the activity and add suggested actions.
        /// var activity = MessageFactory.SuggestedActions(
        ///     new string[] { "red", "green", "blue" },
        ///     text: "Choose a color");
        ///
        /// // Send the activity as a reply to the user.
        /// await context.SendActivity(activity);
        /// </code>
        /// </example>
        /// <param name="actions">
        /// The text of the actions to create.
        /// </param>
        /// <param name="text">The text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <param name="toList">Optional, the list of recipients.</param>
        /// <returns>A message activity containing the suggested actions.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="actions"/> is <c>null</c>.</exception>
        /// <remarks>This method creates a suggested action for each string in <paramref name="actions"/>.
        /// The created action uses the text for the <see cref="Microsoft.Agents.Core.Models.CardAction.Value"/> and
        /// <see cref="Microsoft.Agents.Core.Models.CardAction.Title"/> and sets the <see cref="Microsoft.Agents.Core.Models.CardAction.Type"/> to
        /// <see cref="Microsoft.Agents.Core.Models.ActionTypes.ImBack"/>.
        /// </remarks>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.SuggestedActions(System.Collections.Generic.IEnumerable{Microsoft.Agents.Core.Models.CardAction}, string, string, string, System.Collections.Generic.IList{string})"/>
        public static IActivity SuggestedActions(IEnumerable<string> actions, string text = null, string ssml = null, string inputHint = null, IList<string> toList = default)
        {
            actions = actions ?? throw new ArgumentNullException(nameof(actions));

            var cardActions = new List<CardAction>();
            foreach (var s in actions)
            {
                var ca = new CardAction
                {
                    Type = ActionTypes.ImBack,
                    Value = s,
                    Title = s,
                };

                cardActions.Add(ca);
            }

            return SuggestedActions(cardActions, text, ssml, inputHint, toList);
        }

        /// <summary>
        /// Returns a message that includes a set of suggested actions and optional text.
        /// </summary>
        /// <example>
        /// <code>
        /// // Create the activity and add suggested actions.
        /// var activity = MessageFactory.SuggestedActions(
        ///     new CardAction[]
        ///     {
        ///         new CardAction(title: "red", type: ActionTypes.ImBack, value: "red"),
        ///         new CardAction( title: "green", type: ActionTypes.ImBack, value: "green"),
        ///         new CardAction(title: "blue", type: ActionTypes.ImBack, value: "blue")
        ///     }, text: "Choose a color");
        ///
        /// // Send the activity as a reply to the user.
        /// await context.SendActivity(activity);
        /// </code>
        /// </example>
        /// <param name="cardActions">
        /// The card actions to include.
        /// </param>
        /// <param name="text">Optional, the text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <returns>A message activity that contains the suggested actions.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="cardActions"/> is <c>null</c>.</exception>
        public static IActivity SuggestedActions(IEnumerable<CardAction> cardActions, string text = null, string ssml = null, string inputHint = null)
        {
            return SuggestedActions(cardActions, text, ssml, inputHint, null);
        }

        /// <summary>
        /// Returns a message that includes a set of suggested actions and optional text.
        /// </summary>
        /// <example>
        /// <code>
        /// // Create the activity and add suggested actions.
        /// var activity = MessageFactory.SuggestedActions(
        ///     new CardAction[]
        ///     {
        ///         new CardAction(title: "red", type: ActionTypes.ImBack, value: "red"),
        ///         new CardAction( title: "green", type: ActionTypes.ImBack, value: "green"),
        ///         new CardAction(title: "blue", type: ActionTypes.ImBack, value: "blue")
        ///     }, text: "Choose a color");
        ///
        /// // Send the activity as a reply to the user.
        /// await context.SendActivity(activity);
        /// </code>
        /// </example>
        /// <param name="cardActions">
        /// The card actions to include.
        /// </param>
        /// <param name="text">Optional, the text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <param name="toList">Optional, the list of recipients.</param>
        /// <returns>A message activity that contains the suggested actions.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="cardActions"/> is <c>null</c>.</exception>
        public static IActivity SuggestedActions(IEnumerable<CardAction> cardActions, string text = null, string ssml = null, string inputHint = null, IList<string> toList = default)
        {
            cardActions = cardActions ?? throw new ArgumentNullException(nameof(cardActions));

            var ma = CreateMessageActivity();
            SetTextAndSpeak(ma, text, ssml, inputHint);

            ma.SuggestedActions = new SuggestedActions { Actions = cardActions.ToList(), To = toList };

            return ma;
        }

        /// <summary>
        /// Returns a message activity that contains an attachment.
        /// </summary>
        /// <param name="attachment">Attachment to include in the message.</param>
        /// <param name="text">Optional, the text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <returns>A message activity containing the attachment.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="attachment"/> is <c>null</c>.</exception>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.Attachment(System.Collections.Generic.IEnumerable{Microsoft.Agents.Core.Models.Attachment}, string, string, string)"/>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.Carousel(System.Collections.Generic.IEnumerable{Microsoft.Agents.Core.Models.Attachment}, string, string, string)"/>
        public static IActivity Attachment(Attachment attachment, string text = null, string ssml = null, string inputHint = null)
        {
            attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));

            return Attachment([attachment], text, ssml, inputHint);
        }

        /// <summary>
        /// Returns a message activity that contains a collection of attachments, in a list.
        /// </summary>
        /// <param name="attachments">The attachments to include in the message.</param>
        /// <param name="text">Optional, the text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <returns>A message activity containing the attachment.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="attachments"/> is <c>null</c>.</exception>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.Carousel(System.Collections.Generic.IEnumerable{Microsoft.Agents.Core.Models.Attachment}, string, string, string)"/>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.Attachment(Microsoft.Agents.Core.Models.Attachment, string, string, string)"/>
        public static IActivity Attachment(IEnumerable<Attachment> attachments, string text = null, string ssml = null, string inputHint = null)
        {
            attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));

            return AttachmentActivity(AttachmentLayoutTypes.List.ToString(), attachments, text, ssml, inputHint);
        }

        /// <summary>
        /// Returns a message activity that contains a collection of attachments, as a carousel.
        /// </summary>
        /// <param name="attachments">The attachments to include in the message.</param>
        /// <param name="text">Optional, the text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is "acceptingInput".</param>
        /// <returns>A message activity containing the attachment.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="attachments"/> is <c>null</c>.</exception>
        /// <example>This code creates and sends a carousel of HeroCards.
        /// <code>
        /// // Create the activity and attach a set of Hero cards.
        /// var activity = MessageFactory.Carousel(
        /// new Attachment[]
        /// {
        ///     new HeroCard(
        ///         title: "title1",
        ///         images: new CardImage[] { new CardImage(url: "imageUrl1.png") },
        ///         buttons: new CardAction[]
        ///         {
        ///             new CardAction(title: "button1", type: ActionTypes.ImBack, value: "item1")
        ///         })
        ///     .ToAttachment(),
        ///     new HeroCard(
        ///         title: "title2",
        ///         images: new CardImage[] { new CardImage(url: "imageUrl2.png") },
        ///         buttons: new CardAction[]
        ///         {
        ///             new CardAction(title: "button2", type: ActionTypes.ImBack, value: "item2")
        ///         })
        ///     .ToAttachment(),
        ///     new HeroCard(
        ///         title: "title3",
        ///         images: new CardImage[] { new CardImage(url: "imageUrl3.png") },
        ///         buttons: new CardAction[]
        ///         {
        ///             new CardAction(title: "button3", type: ActionTypes.ImBack, value: "item3")
        ///         })
        ///     .ToAttachment()
        /// });
        ///
        /// // Send the activity as a reply to the user.
        /// await context.SendActivity(activity);
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Agents.Core.Models.MessageFactory.Attachment(System.Collections.Generic.IEnumerable{Microsoft.Agents.Core.Models.Attachment}, string, string, string)"/>
        public static IActivity Carousel(IEnumerable<Attachment> attachments, string text = null, string ssml = null, string inputHint = null)
        {
            attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));

            return AttachmentActivity(AttachmentLayoutTypes.Carousel.ToString(), attachments, text, ssml, inputHint);
        }

        /// <summary>
        /// Returns a message activity that contains a single image or video.
        /// </summary>
        /// <param name="url">The URL of the image or video to send.</param>
        /// <param name="contentType">The MIME type of the image or video.</param>
        /// <param name="name">Optional, the name of the image or video file.</param>
        /// <param name="text">Optional, the text of the message to send.</param>
        /// <param name="ssml">Optional, text to be spoken by your Agent on a speech-enabled
        /// channel.</param>
        /// <param name="inputHint">Optional, indicates whether your Agent is accepting,
        /// expecting, or ignoring user input after the message is delivered to the client.
        /// One of: "acceptingInput", "ignoringInput", or "expectingInput".
        /// Default is null.</param>
        /// <returns>A message activity containing the attachment.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="url"/> or <paramref name="contentType"/> is <c>null</c>,
        /// empty, or white space.</exception>
        /// <example>This code creates a message activity that contains an image.
        /// <code>
        /// Activity message =
        ///     MessageFactory.ContentUrl("https://{domainName}/cat.jpg", MediaTypeNames.Image.Jpeg, "Cat Picture");
        /// </code>
        /// </example>
        public static IActivity ContentUrl(string url, string contentType, string name = null, string text = null, string ssml = null, string inputHint = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            var a = new Attachment
            {
                ContentType = contentType,
                ContentUrl = url,
                Name = !string.IsNullOrWhiteSpace(name) ? name : string.Empty,
            };

            return AttachmentActivity(AttachmentLayoutTypes.List.ToString(), [a], text, ssml, inputHint);
        }
        public static IActivity CreateMessageActivity()
        {
            return new Activity()
            {
                Type = ActivityTypes.Message,
                Attachments = [],
                Entities = [],
            };
        }

        public static IActivity CreateMessageActivity( string messageText , string textFormat = TextFormatTypes.Plain)
        {
            return new Activity()
            {
                Type = ActivityTypes.Message,
                Attachments = [],
                Entities = [],
                Text = messageText,
                TextFormat = textFormat
            };
        }

        public static Activity CreateTrace(IActivity activity, string name, object value = null, string valueType = null, [CallerMemberName] string label = null)
        {
            var reply = new Activity
            {
                Type = ActivityTypes.Trace,
                Timestamp = DateTime.UtcNow,
                From = new ChannelAccount(id: activity.Recipient?.Id, name: activity.Recipient?.Name),
                Recipient = new ChannelAccount(id: activity.From?.Id, name: activity.From?.Name),
                ReplyToId = !activity.IsType(ActivityTypes.ConversationUpdate) || activity.ChannelId != "directline" && activity.ChannelId != "webchat" ? activity.Id : null,
                ServiceUrl = activity.ServiceUrl,
                ChannelId = activity.ChannelId,
                Conversation = activity.Conversation,
                Name = name,
                Label = label,
                ValueType = valueType ?? value?.GetType().Name,
                Value = value,
            };
            return reply;
        }

        private static IActivity AttachmentActivity(string attachmentLayout, IEnumerable<Attachment> attachments, string text = null, string ssml = null, string inputHint = null)
        {
            var ma = CreateMessageActivity();
            ma.AttachmentLayout = attachmentLayout;
            ma.Attachments = attachments.ToList();
            SetTextAndSpeak(ma, text, ssml, inputHint);
            return ma;
        }

        private static void SetTextAndSpeak(IActivity ma, string text = null, string ssml = null, string inputHint = null)
        {
            // Note: we must put NULL in the fields, as the clients will happily render
            // an empty string, which is not the behavior people expect to see.
            ma.Text = !string.IsNullOrWhiteSpace(text) ? text : null;
            ma.Speak = !string.IsNullOrWhiteSpace(ssml) ? ssml : null;
            ma.InputHint = inputHint ?? InputHints.AcceptingInput;
        }
    }
}
