// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Mention information (entity type: "mention"). </summary>
    public class Mention : Entity
    {
        /// <summary> Initializes a new instance of Mention. </summary>
        public Mention() : base(EntityTypes.Mention)
        {
        }

        /// <summary> Initializes a new instance of Mention. </summary>
        /// <param name="type"> Type of this entity (RFC 3987 IRI). </param>
        /// <param name="mentioned"> Channel account information needed to route a message. </param>
        /// <param name="text"> Sub Text which represents the mention (can be null or empty). </param>
        public Mention(ChannelAccount mentioned = default, string text = default, string type = default) : base(type ?? EntityTypes.Mention)
        {
            Mentioned = mentioned;
            Text = text;
        }

        /// <summary> Channel account information needed to route a message. </summary>
        public ChannelAccount Mentioned { get; set; }
        /// <summary> Sub Text which represents the mention (can be null or empty). </summary>
        public string Text { get; set; }
    }
}
