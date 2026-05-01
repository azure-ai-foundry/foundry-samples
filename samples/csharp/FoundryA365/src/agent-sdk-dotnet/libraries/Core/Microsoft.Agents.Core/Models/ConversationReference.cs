// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// The conversationReference type contains a reference into another conversation. In its most minimal form, this reference 
    /// may only contain the IDs of the target conversation. Implementers may wish to carry additional information in the 
    /// conversationReference, such as the identity and roles of participants, and the ID of a specific activity within the 
    /// conversation. Consumers of the conversationReference type are not provided any de facto guarantees about the validity 
    /// or consistency of the IDs within the object; this is instead conferred by the sender who created the object.
    ///
    /// The conversationReference type is frequently used to store a reference to a conversation so it can be later retrieved 
    /// and used to continue a conversation.
    /// </summary>
    public class ConversationReference
    {
        /// <summary> Initializes a new instance of ConversationReference. </summary>
        public ConversationReference()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.ConversationReference"/> class.</summary>
        /// <param name="activityId">(Optional) ID of the activity to refer to.</param>
        /// <param name="user">(Optional) User participating in this conversation.</param>
        /// <param name="agent">Agent participating in this conversation.</param>
        /// <param name="conversation">Conversation reference.</param>
        /// <param name="channelId">Channel ID.</param>
        /// <param name="serviceUrl">Service endpoint where operations concerning the referenced conversation may be performed.</param>
        public ConversationReference(string activityId = default, ChannelAccount user = default, ChannelAccount agent = default, ConversationAccount conversation = default, string channelId = default, string serviceUrl = default)
                : this(default, activityId, user, agent, conversation, channelId, serviceUrl)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.ConversationReference"/> class.</summary>
        /// <param name="locale">
        /// A locale name for the contents of the text field.
        /// The locale name is a combination of an ISO 639 two- or three-letter culture code associated with a language
        /// and an ISO 3166 two-letter subculture code associated with a country or region.
        /// The locale name can also correspond to a valid BCP-47 language tag.
        /// </param>
        /// <param name="activityId">(Optional) ID of the activity to refer to.</param>
        /// <param name="user">(Optional) User participating in this conversation.</param>
        /// <param name="agent">Agent participating in this conversation.</param>
        /// <param name="conversation">Conversation reference.</param>
        /// <param name="channelId">Channel ID.</param>
        /// <param name="serviceUrl">Service endpoint where operations concerning the referenced conversation may be performed.</param>
        public ConversationReference(CultureInfo locale, string activityId = default, ChannelAccount user = default, ChannelAccount agent = default, ConversationAccount conversation = default, string channelId = default, string serviceUrl = default)
        {
            ActivityId = activityId;
            User = user;
            Agent = agent;
            Conversation = conversation;
            ChannelId = channelId;
            Locale = locale?.ToString();
            ServiceUrl = serviceUrl;
        }

        /// <summary>
        /// Creates <see cref="Microsoft.Agents.Core.Models.Activity"/> from conversation reference as it is posted to an Agent.
        /// </summary>
        /// <returns>Continuation activity.</returns>
        public Activity GetContinuationActivity()
        {
            return new Activity()
            {
                Type = ActivityTypes.Event,
                Name = ActivityEventNames.ContinueConversation,
                Id = ActivityId ?? Guid.NewGuid().ToString(),
                ChannelId = ChannelId,
                Locale = Locale,
                DeliveryMode = DeliveryMode,
                ServiceUrl = ServiceUrl,
                Conversation = Conversation,
                Recipient = Agent,
                From = User,
                RelatesTo = this,
                RequestId = RequestId,
            };
        }

        public Activity GetCreateContinuationActivity(ConversationAccount conversation = null, object channelData = null)
        {
            return new Activity()
            {
                Type = ActivityTypes.Event,
                Name = ActivityEventNames.CreateConversation,
                ChannelId = ChannelId,
                Locale = Locale,
                ServiceUrl = ServiceUrl,
                Conversation = conversation ?? Conversation,
                ChannelData = channelData,
                Recipient = Agent,
                From = User ?? Agent,
                RelatesTo = this,
                RequestId = RequestId,
            };
        }

        /// <summary> (Optional) ID of the activity to refer to. </summary>
        public string ActivityId { get; set; }

        /// <summary> Channel account information needed to route a message. </summary>
        public ChannelAccount User { get; set; }

        /// <summary> Channel account information needed to route a message. </summary>
        [JsonPropertyName("bot")]
        public ChannelAccount Agent { get; set; }

        /// <summary> Conversation account represents the identity of the conversation within a channel. </summary>
        public ConversationAccount Conversation { get; set; }

        /// <summary> ID of the channel in which the referenced conversation exists. </summary>
        public string ChannelId { get; set; }

        /// <summary> (Optional) Service endpoint where operations concerning the referenced conversation may be performed. </summary>
        public string ServiceUrl { get; set; }

        /// <summary> (Optional) A BCP-47 locale name for the referenced conversation. </summary>
        public string Locale { get; set; }

        public string RequestId { get; set; }

        public string DeliveryMode { get; set; }
    }
}
