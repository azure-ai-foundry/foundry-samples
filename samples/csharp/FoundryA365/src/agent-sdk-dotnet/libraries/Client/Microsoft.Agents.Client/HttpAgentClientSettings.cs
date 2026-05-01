// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Client.Errors;
using System;

namespace Microsoft.Agents.Client
{
    public class HttpAgentClientSettings : AgentClientSettings
    {
        public ConnectionSettings ConnectionSettings { get; set; } = new ConnectionSettings();


        public override void ValidateClientSettings()
        {
            base.ValidateClientSettings();

            if (string.IsNullOrWhiteSpace(ConnectionSettings.ClientId))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentMissingProperty, null, Name, $"Agent:{Name}:ConnectionSettings:{nameof(ConnectionSettings.ClientId)}");
            }

            if (ConnectionSettings.Endpoint == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentMissingProperty, null, Name, $"Agent:{Name}:ConnectionSettings:{nameof(ConnectionSettings.Endpoint)}");
            }

            if (string.IsNullOrWhiteSpace(ConnectionSettings.TokenProvider))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentMissingProperty, null, Name, $"Agent:{Name}:ConnectionSettings:{nameof(ConnectionSettings.TokenProvider)}");
            }

            if (string.IsNullOrWhiteSpace(ConnectionSettings.ServiceUrl))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentMissingProperty, null, Name, $"Agent:{Name}:ConnectionSettings:{nameof(ConnectionSettings.ServiceUrl)}");
            }

            if (string.IsNullOrEmpty(ConnectionSettings.ResourceUrl))
            {
                ConnectionSettings.ResourceUrl = $"api://{ConnectionSettings.ClientId}";
            }
        }
    }

    public class ConnectionSettings
    {
        /// <summary>
        /// Gets or sets clientId/appId of the Agent.
        /// </summary>
        /// <value>
        /// ClientId/AppId of the Agent.
        /// </value>
        public string ClientId { get; set; }

        public string ResourceUrl { get; set; }

        /// <summary>
        /// Gets or sets provider name for tokens.
        /// </summary>
        public string TokenProvider { get; set; }

        /// <summary>
        /// Gets or sets endpoint for the Agent.
        /// </summary>
        /// <value>
        /// Uri for the Agent.
        /// </value>
        public Uri Endpoint { get; set; }

        public string ServiceUrl { get; set; }
    }
}
