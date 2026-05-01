// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// A builder class for simplifying the creation of an AgentApplication instance.
    /// </summary>
    public class AgentApplicationBuilder
    {
        /// <summary>
        /// Creates the builder and uses IStorage to create the default TurnStateFactory to use for managing the Agent's turn state.
        /// </summary>
        /// <param name="storage">The <see cref="Microsoft.Agents.Storage.IStorage"/> to use with <see cref="Microsoft.Agents.Builder.State.TurnState"/>.</param>
        /// See MemoryStorage, BlobsStorage, or CosmosDbStorage.
        public AgentApplicationBuilder(IStorage storage)
        {
            Options = new(storage);
        }

        /// <summary>
        /// Creates the builder and uses the passed TurnStateFactory to use for managing the Agent's turn state.
        /// </summary>
        /// <param name="turnStateFactory"></param>
        /// See MemoryStorage, BlobsStorage, or CosmosDbStorage.
        public AgentApplicationBuilder(TurnStateFactory turnStateFactory)
        {
            Options = new(turnStateFactory);
        }

        /// <summary>
        /// The application's configured options.
        /// </summary>
        public AgentApplicationOptions Options { get; private set; }

        /// <summary>
        /// Configures the processing of Adaptive Card requests.
        /// </summary>
        /// <param name="adaptiveCardOptions">The options for Adaptive Cards.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public AgentApplicationBuilder WithAdaptiveCardOptions(AdaptiveCardsOptions adaptiveCardOptions)
        {
            Options.AdaptiveCards = adaptiveCardOptions;
            return this;
        }

        /// <summary>
        /// Configures the removing of mentions of the Agent's name from incoming messages.
        /// Default state for removeRecipientMention is true
        /// </summary>
        /// <param name="removeRecipientMention">The boolean for removing recipient mentions.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public AgentApplicationBuilder SetRemoveRecipientMention(bool removeRecipientMention)
        {
            Options.RemoveRecipientMention = removeRecipientMention;
            return this;
        }

        /// <summary>
        /// Configures the normalization of mentions for incoming messages.
        /// Default state for normalizeMentions is true
        /// </summary>
        /// <param name="normalizeMentions">The boolean for normalizing recipient mentions.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public AgentApplicationBuilder SetNormalizeMentions(bool normalizeMentions)
        {
            Options.NormalizeMentions = normalizeMentions;
            return this;
        }

        /// <summary>
        /// Configures the typing timer when messages are received.
        /// Default state for startTypingTimer is true
        /// </summary>
        /// <param name="startTypingTimer">The boolean for starting the typing timer.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public AgentApplicationBuilder SetStartTypingTimer(bool startTypingTimer)
        {
            Options.StartTypingTimer = startTypingTimer;
            return this;
        }

        /// <summary>
        /// Configures the processing of file download requests.
        /// This allows the application to handle file downloads from user messages.
        /// The file downloaders are used to process files that are sent by users in the chat.
        /// They can be used to download files from various sources, such as URLs or local paths.
        /// </summary>
        /// <param name="fileDownloaders">A list of file downloaders.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public AgentApplicationBuilder WithFileDownloaders(IList<IInputFileDownloader> fileDownloaders)
        {
            Options.FileDownloaders = fileDownloaders;
            return this;
        }

        /// <summary>
        /// Configures user authorization for the application.
        /// </summary>
        /// <param name="channelAdapter"></param>
        /// <param name="authorizationOptions">The options for user authorization.</param>
        /// <returns>The ApplicationBuilder instance.</returns>
        public AgentApplicationBuilder WithAuthorization(IChannelAdapter channelAdapter, UserAuthorizationOptions authorizationOptions)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Options.Adapter = channelAdapter;
#pragma warning restore CS0618 // Type or member is obsolete
            Options.UserAuthorization = authorizationOptions;
            return this;
        }

        /// <summary>
        /// Builds and returns a new Application instance.
        /// </summary>
        /// <returns>The Application instance.</returns>
        public AgentApplication Build()
        {
            return new AgentApplication(Options);
        }
    }
}
