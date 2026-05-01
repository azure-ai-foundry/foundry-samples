// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Represents the reaction of a user to a message.
    /// </summary>
    public class MessageActionsPayloadReaction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessageActionsPayloadReaction"/> class.
        /// </summary>
        public MessageActionsPayloadReaction()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.MessageActionsPayloadReaction"/> class.
        /// </summary>
        /// <param name="reactionType">The type of reaction given to the
        /// message. Possible values include: 'like', 'heart', 'laugh',
        /// 'surprised', 'sad', 'angry'.</param>
        /// <param name="createdDateTime">Timestamp of when the user reacted to
        /// the message.</param>
        /// <param name="user">The user with which the reaction is
        /// associated.</param>
        public MessageActionsPayloadReaction(string reactionType = default, string createdDateTime = default, MessageActionsPayloadFrom user = default)
        {
            ReactionType = reactionType;
            CreatedDateTime = createdDateTime;
            User = user;
        }

        /// <summary>
        /// Gets or sets the type of reaction given to the message. Possible
        /// values include: 'like', 'heart', 'laugh', 'surprised', 'sad',
        /// 'angry'.
        /// </summary>
        /// <value>The reaction type given to the message.</value>
        public string ReactionType { get; set; }

        /// <summary>
        /// Gets or sets timestamp of when the user reacted to the message.
        /// </summary>
        /// <value>The timestamp of when the user reacted to the message.</value>
        public string CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the user with which the reaction is associated.
        /// </summary>
        /// <value>The user with which the reaction is associated.</value>
        public MessageActionsPayloadFrom User { get; set; }
    }
}
