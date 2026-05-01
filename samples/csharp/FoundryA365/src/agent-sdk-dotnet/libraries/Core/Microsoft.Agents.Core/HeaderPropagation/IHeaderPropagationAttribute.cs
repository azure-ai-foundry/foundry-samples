// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.HeaderPropagation;

#if !NETSTANDARD
/// <summary>
/// Interface to ensure that the header propagation attribute is implemented correctly.
/// </summary>
public interface IHeaderPropagationAttribute
{
    /// <summary>
    /// Loads the header entries into the outgoing request headers collection.
    /// </summary>
    /// <param name="collection">A collection to operate over the headers.</param>
    abstract static void LoadHeaders(HeaderPropagationEntryCollection collection);
}
#endif