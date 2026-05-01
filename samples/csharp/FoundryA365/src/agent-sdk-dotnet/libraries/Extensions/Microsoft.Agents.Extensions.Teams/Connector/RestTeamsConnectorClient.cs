// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using System;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    /// <summary>
    /// TeamsConnectorClient REST implementation.
    /// </summary>
    public class RestTeamsConnectorClient : ITeamsConnectorClient
    {
        public RestTeamsConnectorClient(IConnectorClient connector, IRestTransport transport = null)
        {
            var restTransport = transport ?? connector as IRestTransport;
            AssertionHelpers.ThrowIfNull(restTransport, nameof(connector));
            Teams = new RestTeamsOperations(restTransport);
        }

        /// <inheritdoc/>
        public ITeamsOperations Teams { get; private set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
