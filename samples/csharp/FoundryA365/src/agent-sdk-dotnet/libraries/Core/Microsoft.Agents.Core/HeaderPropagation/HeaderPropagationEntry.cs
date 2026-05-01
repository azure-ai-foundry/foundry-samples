// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Primitives;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Represents a single header entry used for header propagation.
/// </summary>
public class HeaderPropagationEntry
{
    /// <summary>
    /// Key of the header entry.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Value of the header entry.
    /// </summary>
    public StringValues Value { get; set; } = new StringValues(string.Empty);

    /// <summary>
    /// Action of the header entry (Add, Append, etc.).
    /// </summary>
    public HeaderPropagationEntryAction Action;
}
