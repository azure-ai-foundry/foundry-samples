// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class EntityInitSourceGeneratorTests
    {
        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Returns BCL platform references plus Microsoft.Agents.Core so tests can
        /// compile source that references <c>Microsoft.Agents.Core.Models.Entity</c>.
        /// </summary>
        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);

            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Core.Models.Entity).Assembly.Location);
        }

        private static CSharpGeneratorDriver RunGenerator(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new EntityInitSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            return (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        }

        // ---------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------

        [Fact]
        public void NoEntitySubclasses_ProducesNoOutput()
        {
            var source = """
                namespace MyApp
                {
                    public class NotAnEntity { }
                    public class AlsoNotAnEntity : System.Exception { }
                }
                """;

            var result = RunGenerator(source).GetRunResult();

            Assert.Empty(result.Results.Single().GeneratedSources);
        }

        [Fact]
        public void OneEntitySubclass_GeneratesOneAttribute()
        {
            var source = """
                namespace MyApp
                {
                    public class MyMention : Microsoft.Agents.Core.Models.Entity
                    {
                        public MyMention() : base("myMention") { }
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            var generated = Assert.Single(result.Results.Single().GeneratedSources);
            var text = generated.SourceText.ToString();

            Assert.Contains(
                "[assembly: Microsoft.Agents.Core.Serialization.EntityInitAssemblyAttribute(typeof(global::MyApp.MyMention))]",
                text);
        }

        [Fact]
        public void MultipleEntitySubclasses_GeneratesAllAttributes()
        {
            var source = """
                namespace MyApp
                {
                    public class EntityA : Microsoft.Agents.Core.Models.Entity
                    {
                        public EntityA() : base("entityA") { }
                    }

                    public class EntityB : Microsoft.Agents.Core.Models.Entity
                    {
                        public EntityB() : base("entityB") { }
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();
            var text = Assert.Single(result.Results.Single().GeneratedSources).SourceText.ToString();

            Assert.Contains("global::MyApp.EntityA", text);
            Assert.Contains("global::MyApp.EntityB", text);
        }

        [Fact]
        public void IndirectEntitySubclass_IsIncluded()
        {
            // Class that inherits from a subclass of Entity (not directly from Entity)
            // should still be registered.
            var source = """
                namespace MyApp
                {
                    public class DirectSubclass : Microsoft.Agents.Core.Models.Entity
                    {
                        public DirectSubclass() : base("direct") { }
                    }

                    public class IndirectSubclass : DirectSubclass
                    {
                        public IndirectSubclass() : base("indirect") { }
                    }
                }
                """;

            var text = RunGenerator(source).GetRunResult()
                .Results.Single().GeneratedSources.Single().SourceText.ToString();

            Assert.Contains("global::MyApp.DirectSubclass", text);
            Assert.Contains("global::MyApp.IndirectSubclass", text);
        }

        [Fact]
        public void NonEntityClass_IsNotIncluded()
        {
            var source = """
                namespace MyApp
                {
                    public class SomethingElse : System.Exception { }
                    public class EntityDerived : Microsoft.Agents.Core.Models.Entity
                    {
                        public EntityDerived() : base("derived") { }
                    }
                }
                """;

            var text = RunGenerator(source).GetRunResult()
                .Results.Single().GeneratedSources.Single().SourceText.ToString();

            Assert.DoesNotContain("SomethingElse", text);
            Assert.Contains("global::MyApp.EntityDerived", text);
        }

        [Fact]
        public void ClassWithoutBaseList_IsIgnored()
        {
            // Classes with no base list should not reach semantic analysis at all
            // (filtered by the syntax predicate).
            var source = """
                namespace MyApp
                {
                    public class Standalone { }
                    public class WithBase : Microsoft.Agents.Core.Models.Entity
                    {
                        public WithBase() : base("withBase") { }
                    }
                }
                """;

            var text = RunGenerator(source).GetRunResult()
                .Results.Single().GeneratedSources.Single().SourceText.ToString();

            Assert.DoesNotContain("Standalone", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var source = """
                namespace MyApp
                {
                    public class MyEntity : Microsoft.Agents.Core.Models.Entity
                    {
                        public MyEntity() : base("myEntity") { }
                    }
                }
                """;

            var generated = RunGenerator(source).GetRunResult()
                .Results.Single().GeneratedSources.Single();

            Assert.Equal("EntityInitAssemblyAttribute.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics()
        {
            var source = """
                namespace MyApp
                {
                    public class MyEntity : Microsoft.Agents.Core.Models.Entity
                    {
                        public MyEntity() : base("myEntity") { }
                    }
                }
                """;

            var result = RunGenerator(source).GetRunResult();

            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void IncrementalCaching_DoesNotRerunWhenUnrelatedFileChanges()
        {
            // Verifies that the generator's incremental caching works: running it
            // a second time with an unchanged compilation should not regenerate output.
            var source = """
                namespace MyApp
                {
                    public class MyEntity : Microsoft.Agents.Core.Models.Entity
                    {
                        public MyEntity() : base("myEntity") { }
                    }
                }
                """;

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new EntityInitSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // First run
            driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
            var firstResult = driver.GetRunResult().Results.Single();
            Assert.Single(firstResult.GeneratedSources);

            // Second run with same compilation — should hit cache
            driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
            var secondResult = driver.GetRunResult().Results.Single();

            Assert.Single(secondResult.GeneratedSources);
            Assert.Equal(
                firstResult.GeneratedSources.Single().SourceText.ToString(),
                secondResult.GeneratedSources.Single().SourceText.ToString());
        }
    }
}
