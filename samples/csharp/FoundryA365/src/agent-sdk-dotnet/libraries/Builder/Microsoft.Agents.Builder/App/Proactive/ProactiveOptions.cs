// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Builder.App.Proactive
{
    /// <summary>
    /// Provides configuration options for proactive features, including storage settings.
    /// </summary>
    public class ProactiveOptions
    {
        /// <summary>
        /// Initializes a new instance of the ProactiveOptions class using the specified storage and optional
        /// configuration settings.
        /// </summary>
        /// <remarks>If a configuration source is provided and contains the specified section, relevant
        /// options such as FailOnUnsignedInConnections are loaded from that section. Otherwise, default values are
        /// used.</remarks>
        /// <param name="storage">The storage provider used to persist proactive options. Cannot be null.</param>
        /// <param name="configuration">An optional configuration source from which to read additional settings. If null, configuration values are
        /// not loaded.</param>
        /// <param name="configKey">The configuration section key to use when retrieving settings from the configuration source. Defaults to
        /// "Proactive".</param>
        public ProactiveOptions(IStorage storage, IConfiguration configuration = null, string configKey = "Proactive")
        {
            Storage = storage;
            if (configuration != null)
            {
                var section = configuration.GetSection(configKey);
                if (section.Exists())
                {
                    FailOnUnsignedInConnections = section.GetValue<bool>(nameof(FailOnUnsignedInConnections));
                }
            }
        }

        /// <summary>
        /// Gets or sets the storage provider used for data persistence operations.
        /// </summary>
        public IStorage Storage { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether to fail ContinueConversation when any in the list of
        /// token handlers is not signed in.
        /// </summary>
        public bool FailOnUnsignedInConnections { get; set; } = true;
    }
}
