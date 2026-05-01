// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> State object passed to the Token Service. </summary>
    public class TokenExchangeState
    {
        /// <summary> Initializes a new instance of TokenExchangeState. </summary>
        public TokenExchangeState()
        {
        }

        /// <summary> Initializes a new instance of TokenExchangeState. </summary>
        /// <param name="connectionName"> The connection name that was used. </param>
        /// <param name="conversation"> An object relating to a particular point in a conversation. </param>
        /// <param name="relatesTo"> An object relating to a particular point in a conversation. </param>
        /// <param name="msAppId"> The Agent's registered application ID. </param>
        internal TokenExchangeState(string connectionName, ConversationReference conversation, ConversationReference relatesTo, string msAppId)
        {
            ConnectionName = connectionName;
            Conversation = conversation;
            RelatesTo = relatesTo;
            MsAppId = msAppId;
        }

        /// <summary> The connection name that was used. </summary>
        public string ConnectionName { get; set; }
        /// <summary> An object relating to a particular point in a conversation. </summary>
        public ConversationReference Conversation { get; set; }
        /// <summary> An object relating to a particular point in a conversation. </summary>
        public ConversationReference RelatesTo { get; set; }
        /// <summary> The Agent's registered application ID. </summary>
        public string MsAppId { get; set; }

        /// <summary>
        /// Gets or sets the URL of the Agent messaging endpoint.
        /// </summary>
        /// <value>
        /// The URL of the Agent messaging endpoint.
        /// </value>
        [JsonPropertyName("botUrl")]
        public string AgentUrl { get; set; }
    }
}
