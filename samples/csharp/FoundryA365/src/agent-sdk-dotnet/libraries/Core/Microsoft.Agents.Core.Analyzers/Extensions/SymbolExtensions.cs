// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Microsoft.Agents.Core.Analyzers.Extensions
{
    internal static class SymbolExtensions
    {
        public static bool InheritsFrom(this ITypeSymbol classSymbol, ISymbol? baseClassSymbol)
        {
            var current = classSymbol.BaseType;
            while (current != null)
            {
                if (current.Equals(baseClassSymbol, SymbolEqualityComparer.Default))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }
    }
}