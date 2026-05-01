// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using System.Collections.Generic;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Builder.App.Proactive;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// TurnState for an Agent is created at the beginning of a Turn by invoking the specified factory delegate.
    /// </summary>
    /// <remarks>
    /// The default implementation of this is
    /// <code>
    ///    () => new TurnState(service.GetService&lt;IStorage&gt;()
    /// </code>
    /// If <c>IStorage</c> was not DI'd, an (effectively) singleton instance should be used:
    /// <code>
    ///    var storage = new MemoryStorage();  // MemoryStorage for local dev
    ///    services.AddTransient&lt;IAgent&gt;(sp =>
    ///    {
    ///        var options = new AgentApplicationOptions(storage);
    ///        var app = new AgentApplication(options);
    ///
    ///        ...
    ///
    ///        return app;
    ///    });
    /// </code>
    /// </remarks>
    /// <seealso cref="Microsoft.Agents.Builder.State.TurnState"/>
    /// See MemoryStorage, BlobsStorage, or CosmosDbStorage.
    public delegate ITurnState TurnStateFactory();

    /// <summary>
    /// Options for the <see cref="Microsoft.Agents.Builder.App.AgentApplication"/> class.  AgentApplicationOptions can be constructed
    /// via <c>IConfiguration</c> values or programmatically.
    /// </summary>
    /// <seealso cref="Microsoft.Agents.Builder.App.TurnStateFactory"/>
    /// <seealso cref="Microsoft.Agents.Builder.App.UserAuth.UserAuthorizationOptions"/>
    public class AgentApplicationOptions
    {
        internal static readonly ILoggerFactory DefaultLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddFilter("Microsoft.Agents", LogLevel.Warning));

        /// <summary>
        /// Constructs AgentApplicationOptions programmatically.
        /// <code>
        ///   var options = new AgentApplicationOptions(storageInstance)
        ///   {
        ///     StartTypingTimer = true,
        ///     
        ///     ...
        ///     
        ///     UserAuthorization = new UserAuthorizationOptions()   // if required
        ///     {
        ///       ...
        ///     }
        ///     
        ///     AdaptiveCards = new AdaptiveCardsOptions()  // if required
        ///     {
        ///     }
        ///   };
        /// </code>
        /// <remarks>
        /// This will set the `TurnStateFactory` property with the default TurnStateFactory .
        /// </remarks>
        /// </summary>
        public AgentApplicationOptions(IStorage storage, ILoggerFactory loggerFactory = null) : this(storage == null ? () => new TurnState() : () => new TurnState(storage), loggerFactory)
        {
        }

        public AgentApplicationOptions(TurnStateFactory turnStateFactory, ILoggerFactory loggerFactory = null)
        {
            TurnStateFactory = turnStateFactory;
            LoggerFactory = loggerFactory ?? DefaultLoggerFactory;
        }

        /// <summary>
        /// Creates AgentApplicationOptions from IConfiguration and DI.
        /// <code>
        /// "AgentApplication": {
        ///   "StartTypingTimer": false,
        ///   "RemoveRecipientMention": true,
        ///   "NormalizeMentions": true,
        ///   
        ///   "UserAuthorization": {  // omit to disable User Authorization
        ///     "Default": "graph",
        ///     "AutoSignIn": {true | false},
        ///     "Handlers": {
        ///       "graph": {
        ///         "Settings": {
        ///           "AzureBotOAuthConnectionName": null
        ///         }
        ///       }
        ///     }
        ///   },
        ///   
        ///   "AdaptiveCards" : {     // optional
        ///   }
        /// }
        /// </code>
        /// Typically this is used by injection:
        /// <code>
        ///   builder.Services.AddSingleton&lt;AgentApplicationOptions&lt;();
        /// </code>
        /// Or by using the Hosting.AspNetCore extension
        /// <code>
        ///   builder.AddAgentApplicationOptions();
        /// </code>
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="configuration"></param>
        /// <param name="channelAdapter"></param>
        /// <param name="storage">The IStorage used by TurnState and User Authorization.</param>
        /// <param name="authOptions"></param>
        /// <param name="cardOptions"></param>
        /// <param name="fileDownloaders"></param>
        /// <param name="configKey"></param>
        /// <param name="loggerFactory"></param>
        public AgentApplicationOptions(
            IServiceProvider sp,
            IConfiguration configuration, 
            IChannelAdapter channelAdapter, 
            IStorage storage = null, 
            UserAuthorizationOptions authOptions = null,
            AdaptiveCardsOptions cardOptions = null,
            IList<IInputFileDownloader> fileDownloaders = null,
            string configKey = "AgentApplication",
            ILoggerFactory loggerFactory = null) 
        {
            LoggerFactory = loggerFactory ?? DefaultLoggerFactory;

#pragma warning disable CS0618 // Type or member is obsolete
            Adapter = channelAdapter;
#pragma warning restore CS0618 // Type or member is obsolete
            Connections = sp.GetService<IConnections>();

            storage ??= new MemoryStorage();
            TurnStateFactory = () => new TurnState(storage);  // Null storage will just create a TurnState with TempState.

            var section = configuration.GetSection(configKey);
            if (!section.Exists())
            {
                // This is to compensate for IConfiguration containing the class name as the section name.
                section = configuration.GetSection(nameof(AgentApplicationOptions));
            }

            StartTypingTimer = section.GetValue<bool>(nameof(StartTypingTimer), false);
            RemoveRecipientMention = section.GetValue<bool>(nameof(RemoveRecipientMention), true);
            NormalizeMentions = section.GetValue<bool>(nameof(NormalizeMentions), true);

            if (authOptions != null)
            {
                UserAuthorization = authOptions;
            }
            else if (section.GetSection("UserAuthorization").Exists())
            {
                UserAuthorization = new UserAuthorizationOptions(sp, loggerFactory, configuration, storage, configKey: $"{configKey}:UserAuthorization");
            }

            section = section.GetSection("AdaptiveCards");
            if (section.Exists())
            {
                AdaptiveCards = cardOptions ?? section.Get<AdaptiveCardsOptions>();
            }

            Proactive = new ProactiveOptions(storage ?? sp.GetService<IStorage>(), configuration, configKey: $"{configKey}:Proactive");

            // Can't get these from config at the moment
            FileDownloaders = fileDownloaders;
        }

        /// <summary>
        /// The IChannelAdapter to use in cases on proactive.
        /// </summary>
        /// <remarks>
        /// An Adapter would be required to use IChannelAdapter.ContinueConversationAsync or IChannelAdapter.CreateConversation.
        /// </remarks>
        [Obsolete("Use ITurnContext.Adapter property instead.")]
        public IChannelAdapter? Adapter { get; set; }

        /// <summary>
        /// The IConnections for this AgentApplication
        /// </summary>
        public IConnections? Connections { get; set; }

        /// <summary>
        /// Optional. Options used to customize the processing of Adaptive Card requests.
        /// </summary>
        public AdaptiveCardsOptions? AdaptiveCards { get; set; }

        public ProactiveOptions Proactive { get; set; }

        /// <summary>
        /// Optional. Factory used to create a custom turn state instance.
        /// </summary>
        /// <remarks>
        /// Not setting the TurnStateFactory would result in an in-memory <see cref="Microsoft.Agents.Builder.State.TurnState"/> that provides just TempState.  This could
        /// be appropriate for Agents not needing persisted state.
        /// <see cref="Microsoft.Agents.Builder.App.TurnStateFactory"/>
        /// </remarks>
        public TurnStateFactory TurnStateFactory { get; set; }

        /// <summary>
        /// Optional. Array of input file download plugins to use.
        /// </summary>
        public IList<IInputFileDownloader>? FileDownloaders { get; set; }

        /// <summary>
        /// Optional. If true, the Agent will automatically remove mentions of the Agents name from incoming
        /// messages. Defaults to true.
        /// </summary>
        public bool RemoveRecipientMention { get; set; } = true;

        /// <summary>
        /// Optional. If true, the Agent will automatically normalize mentions across channels.
        /// Defaults to true.
        /// </summary>
        public bool NormalizeMentions { get; set; } = true;

        /// <summary>
        /// Optional. If true, the Agent will automatically start a typing timer when messages are received.
        /// This allows the Agent to automatically indicate that it's received the message and is processing
        /// the request. Defaults to false.
        /// </summary>
        public bool StartTypingTimer { get; set; } = false;

        /// <summary>
        /// Optional. Options for controlling typing indicator timing and per-channel behavior.
        /// Only used when <see cref="StartTypingTimer"/> is true.
        /// </summary>
        public TypingOptions TypingOptions { get; set; } = new TypingOptions();

        /// <summary>
        /// Optional. Options used to enable user authorization for the application.
        /// </summary>
        public UserAuthorizationOptions UserAuthorization { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ILoggerFactory LoggerFactory { get; set; }
    }
}