// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Conversation update events.
    /// </summary>
    public class TeamsConversationUpdateEvents : ConversationUpdateEvents
    {
        /// <summary>
        /// ChannelCreated event
        /// </summary>
        public const string ChannelCreated = "channelCreated";

        /// <summary>
        /// ChannelRenamed event
        /// </summary>
        public const string ChannelRenamed = "channelRenamed";

        /// <summary>
        /// ChannelDeleted event
        /// </summary>
        public const string ChannelDeleted = "channelDeleted";

        /// <summary>
        /// ChannelRestored event
        /// </summary>
        public const string ChannelRestored = "channelRestored";

        /// <summary>
        /// TeamRenamed event
        /// </summary>
        public const string TeamRenamed = "teamRenamed";

        /// <summary>
        /// TeamDeleted event
        /// </summary>
        public const string TeamDeleted = "teamDeleted";

        /// <summary>
        /// TeamArchived event
        /// </summary>
        public const string TeamArchived = "teamArchived";

        /// <summary>
        /// TeamUnarchived event
        /// </summary>
        public const string TeamUnarchived = "teamUnarchived";

        /// <summary>
        /// TeamRestored event
        /// </summary>
        public const string TeamRestored = "teamRestored";

        /// <summary>
        /// TeamHardDeleted event
        /// </summary>
        public const string TeamHardDeleted = "teamHardDeleted";

        /// <summary>
        /// TopicName event 
        /// </summary>
        public const string TopicName = "topicName";

        /// <summary>
        /// HistoryDisclosed event
        /// </summary>
        public const string HistoryDisclosed = "historyDisclosed";

        /// <summary>
        /// ChannelShared event
        /// </summary>
        public const string ChannelShared = "channelShared";

        /// <summary>
        /// ChannelUnshared event
        /// </summary>
        public const string ChannelUnshared = "channelUnshared";
    }
}
