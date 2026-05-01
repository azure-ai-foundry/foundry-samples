// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Represents a collection of all the header entries that are to be propagated to the outgoing request.
/// </summary>
public class HeaderPropagationEntryCollection
{
    private readonly Dictionary<string, HeaderPropagationEntry> _entries = [];

    private static readonly object _optionsLock = new object();

    /// <summary>
    /// Gets the collection of header entries to be propagated to the outgoing request.
    /// </summary>
    public List<HeaderPropagationEntry> Entries
    {
        get => [.. _entries.Select(x => x.Value)];
    }

    /// <summary>
    /// Attempts to add a new header entry to the collection.
    /// </summary>
    /// <remarks>
    /// If the key already exists in the incoming request headers collection, it will be ignored.
    /// </remarks>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value to add for the specified key.</param>
    public void Add(string key, StringValues value)
    {
        lock (_optionsLock)
        {
            _entries[key] = new HeaderPropagationEntry
            {
                Key = key,
                Value = value,
                Action = HeaderPropagationEntryAction.Add
            };
        }
    }

    /// <summary>
    /// Appends a new header value to an existing key.
    /// </summary>
    /// <remarks>
    /// If the key does not exist in the incoming request headers collection, it will be ignored.
    /// </remarks>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value to add for the specified key.</param>
    public void Append(string key, StringValues value)
    {
        lock (_optionsLock)
        {
            StringValues newValue = value;

            if (_entries.TryGetValue(key, out var entry))
            {
                // If the key already exists, append the new value to the existing one.
                if (string.Equals(key, "user-agent", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Special handling for User-Agent to concatenate values with a space.
                    newValue = new StringValues(string.Join(" ", [.. entry.Value, value]));
                }
                else
                {
                    // For other headers, use default concat which separate the list with a comma.
                    newValue = StringValues.Concat(entry.Value, value);
                }
            }

            _entries[key] = new HeaderPropagationEntry
            {
                Key = key,
                Value = newValue,
                Action = HeaderPropagationEntryAction.Append
            };
        }
    }

    /// <summary>
    /// Propagates the incoming request header value to the outgoing request.
    /// </summary>
    /// <remarks>
    /// If the key does not exist in the incoming request headers collection, it will be ignored.
    /// </remarks>
    /// <param name="key">The key of the element to add.</param>
    public void Propagate(string key)
    {
        lock (_optionsLock)
        {
            _entries[key] = new HeaderPropagationEntry
            {
                Key = key,
                Action = HeaderPropagationEntryAction.Propagate
            };
        }
    }

    /// <summary>
    /// Overrides the header value of an existing key.
    /// </summary>
    /// <remarks>
    /// If the key does not exist in the incoming request headers collection, it will add it.
    /// </remarks>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value to add for the specified key.</param>
    public void Override(string key, StringValues value)
    {
        lock (_optionsLock)
        {
            _entries[key] = new HeaderPropagationEntry
            {
                Key = key,
                Value = value,
                Action = HeaderPropagationEntryAction.Override
            };
        }
    }
}
