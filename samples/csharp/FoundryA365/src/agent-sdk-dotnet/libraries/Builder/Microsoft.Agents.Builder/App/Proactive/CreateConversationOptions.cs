// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using System.Security.Claims;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Represents the data required to create a new conversation, including authentication scope, configuration
    /// parameters, and conversation reference information.
    /// </summary>
    public class CreateConversationOptions
    {
        public const string AzureBotAudience = AuthenticationConstants.BotFrameworkAudience;
        public const string AzureBotScope = AuthenticationConstants.BotFrameworkDefaultScope;

        /// <summary>
        /// Gets or sets the claims-based identity associated with the current user.
        /// </summary>
        public ClaimsIdentity Identity { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the channel.
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the base URL of the remote service endpoint.
        /// </summary>
        /// <remarks>
        /// If null, the default service URL for the channel will be used.
        /// </remarks>
        public string ServiceUrl { get; set; }

        /// <summary>
        /// Gets or sets the audience to use when requesting authentication tokens for Azure Bot Service.
        /// </summary>
        public string Audience { get; set; } = AzureBotAudience;

        /// <summary>
        /// Gets or sets the OAuth scope to use when requesting authentication tokens.
        /// </summary>
        public string Scope { get; set; } = AzureBotScope;

        /// <summary>
        /// Gets or sets a value indicating whether the conversation should be stored for later retrieval.
        /// </summary>
        public bool StoreConversation { get; set; } = false;

        /// <summary>
        /// Gets or sets the parameters used to configure the conversation.
        /// </summary>
        public ConversationParameters Parameters { get; set; }
    }
}
