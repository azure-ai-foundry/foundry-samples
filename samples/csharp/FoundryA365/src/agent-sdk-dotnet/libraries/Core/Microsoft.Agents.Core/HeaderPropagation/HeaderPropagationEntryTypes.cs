// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Represents the action of the header entry.
/// </summary>
public enum HeaderPropagationEntryAction
{
    /// <summary>
    /// Adds a new header entry to the outgoing request.
    /// </summary>
    [EnumMember(Value = "add")]
    Add,

    /// <summary>
    /// Appends a new header value to an existing key in the outgoing request.
    /// </summary>
    [EnumMember(Value = "append")]
    Append,

    /// <summary>
    /// Propagates the header entry from the incoming request to the outgoing request.
    /// </summary>
    [EnumMember(Value = "propagate")]
    Propagate,

    /// <summary>
    /// Overrides an existing header entry in the outgoing request.
    /// </summary>
    [EnumMember(Value = "override")]
    Override
}
