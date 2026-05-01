// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    /// <summary>
    /// ﻿﻿The Connector for Microsoft Teams allows your Agent to perform extended operations on a Microsoft Teams channel.
    /// </summary>
    public interface ITeamsConnectorClient : IDisposable
    {
        /// <summary>
        /// Gets the ITeamsOperations.
        /// </summary>
        /// <value>The ITeamsOperations.</value>
        ITeamsOperations Teams { get; }
    }
}
