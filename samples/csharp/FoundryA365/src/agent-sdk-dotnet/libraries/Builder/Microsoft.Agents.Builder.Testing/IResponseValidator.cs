// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Testing
{
    /// <summary>
    /// Pluggable async validation abstraction for agent replies.
    /// </summary>
    public interface IResponseValidator
    {
        /// <summary>
        /// Validates <paramref name="reply"/>. Throw to signal validation failure.
        /// </summary>
        Task ValidateAsync(IActivity reply, CancellationToken cancellationToken = default);
    }
}
