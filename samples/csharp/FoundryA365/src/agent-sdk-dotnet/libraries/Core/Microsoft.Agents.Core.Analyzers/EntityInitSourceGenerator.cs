// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Analyzers.Extensions;
using Microsoft.Agents.Core.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Creates assembly EntityInitAssemblyAttribute to register all Entity subclasses for serialization.
    /// </summary>
    [Generator]
    public class EntityInitSourceGenerator : IIncrementalGenerator
    {
        internal const string EntityTypeFullName = "Microsoft.Agents.Core.Models.Entity";
        internal const string EntityInitAssemblyAttributeFullName = "Microsoft.Agents.Core.Serialization.EntityInitAssemblyAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Use SyntaxProvider to filter at the syntax level first (fast, no semantic analysis),
            // then do semantic checks only for classes that actually have a base list.
            // Results are cached per syntax node — only changed classes are re-analyzed.
            var derivedTypes = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();

                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol typeSymbol)
                        return null;

                    var entityType = ctx.SemanticModel.Compilation.GetTypeByMetadataName(EntityTypeFullName);
                    if (entityType is null)
                        return null;

                    return typeSymbol.InheritsFrom(entityType)
                        ? typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        : null;
                })
                .Where(static x => x is not null)
                .Collect()
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
                // Custom comparer expects string?, but we guarantee non-null strings via the Where filter above.
                .WithComparer(new ObjectImmutableArraySequenceEqualityComparer<string>());
#pragma warning restore CS8620

            context.RegisterSourceOutput(derivedTypes, static (spc, types) =>
            {
                if (types.IsDefaultOrEmpty)
                    return;

                var entityAttributes = string.Join("\r\n", types.Distinct().Select(x =>
                    $"[assembly: {EntityInitAssemblyAttributeFullName}(typeof({x}))]"));
                spc.AddSource("EntityInitAssemblyAttribute.g.cs", SourceText.From(entityAttributes, Encoding.UTF8));
            });
        }
    }
}
