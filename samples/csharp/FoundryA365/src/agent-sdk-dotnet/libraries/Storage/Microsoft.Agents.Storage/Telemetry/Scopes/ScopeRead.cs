// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Storage.Telemetry.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Storage.Telemetry.Scopes.ScopeStorageOperation"/> that traces a storage read operation.
    /// </summary>
    public class ScopeRead : ScopeStorageOperation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.Telemetry.Scopes.ScopeRead"/> class.
        /// </summary>
        /// <param name="keyCount">The number of keys being read.</param>
        public ScopeRead(int keyCount) : base(Constants.ScopeRead, Constants.OperationRead, keyCount)
        {
        }
    }
}