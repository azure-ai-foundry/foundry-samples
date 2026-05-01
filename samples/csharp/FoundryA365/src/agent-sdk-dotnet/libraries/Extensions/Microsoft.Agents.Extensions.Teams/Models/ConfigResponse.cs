// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Envelope for Config Response Payload.
    /// </summary>
    /// <typeparam name="T">The first generic type parameter.</typeparam>.
    public class ConfigResponse<T> : ConfigResponseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ConfigResponse{T}"/> class.
        /// </summary>
        public ConfigResponse()
            : base("config")
        {
        }

        /// <summary>
        /// Gets or sets the response to the config message.
        /// Possible values for the config type include: 'auth'or 'task'.
        /// </summary>
        /// <value>
        /// Response to a config request.
        /// </value>
        public T Config { get; set; }

        /// <summary>
        /// Gets or sets response cache Info.
        /// </summary>
        /// <value> Value of cache info. </value>
        public CacheInfo CacheInfo { get; set; }
    }
}
