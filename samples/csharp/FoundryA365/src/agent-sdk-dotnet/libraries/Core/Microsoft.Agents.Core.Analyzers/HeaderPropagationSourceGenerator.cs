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
    /// Creates assembly HeaderPropagationAssemblyAttribute to register all HeaderPropagationAttribute types.
    /// </summary>
    [Generator]
    public class HeaderPropagationSourceGenerator : IIncrementalGenerator
    {
        internal const string HeaderPropagationAttributeFullName = "Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationAttribute";
        internal const string HeaderPropagationAssemblyAttributeFullName = "Microsoft.Agents.Core.HeaderPropagation.HeaderPropagationAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<ImmutableArray<string?>> types =
                context.SyntaxProvider
                    .ForAttributeWithMetadataName(
                        HeaderPropagationAttributeFullName,
                        (node, _) => node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
                        (context, ct) =>
                            (context.TargetSymbol as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    )
                    .Where(static x => x is not null)
                    .Collect();

            context.RegisterSourceOutput(types, static (context, types) =>
            {
                if (types.IsDefaultOrEmpty)
                {
                    return;
                }

                var source = string.Join("\r\n", types.Select(x => $"[assembly: {HeaderPropagationAssemblyAttributeFullName}(typeof({x}))]"));

                context.AddSource("HeaderPropagationAssemblyAttributes.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }
    }
}
