// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Creates assembly SerializationInitAssemblyAttribute to register all SerializationInitAttributes for serialization configuration.
    /// </summary>
    [Generator]
    public class SerializationInitSourceGenerator : IIncrementalGenerator
    {
        internal const string SerializationInitAttributeFullName = "Microsoft.Agents.Core.Serialization.SerializationInitAttribute";
        internal const string SerializationInitAssemblyAttributeFullName = "Microsoft.Agents.Core.Serialization.SerializationInitAssemblyAttribute";    

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ImmutableArray<string?>> types =
                context.SyntaxProvider
                    .ForAttributeWithMetadataName(
                        SerializationInitAttributeFullName,
                        (node, _) => node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
                        (context, ct) =>
                            // If need be - this can also return some diagnostics objects (like a warning if decorated type doesn't have static Init method, etc)
                            // Those diagnostics can be emitted when registering source output below
                            (context.TargetSymbol as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    )
                    .Where(static x => x is not null)
                    .Collect();

            context.RegisterSourceOutput(types, static (context, types) =>
            {
                var source = string.Join("\r\n", types.Select(x => $"[assembly: {SerializationInitAssemblyAttributeFullName}(typeof({x}))]"));

                context.AddSource("SerializationInitAssemblyAttributes.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }
    }
}