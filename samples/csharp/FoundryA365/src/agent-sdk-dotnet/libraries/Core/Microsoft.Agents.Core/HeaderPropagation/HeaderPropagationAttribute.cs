// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Attribute to register a type for header propagation initialization.
/// </summary>
/// <remarks>
/// Should be applied to classes that implement the <see cref="Microsoft.Agents.Core.HeaderPropagation.IHeaderPropagationAttribute"/> interface.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class HeaderPropagationAttribute : Attribute
{
}
