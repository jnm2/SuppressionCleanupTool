using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace SuppressionCleanupTool
{
    public static class BuiltInRoslynAnalyzers
    {
        public static ImmutableArray<AnalyzerReference> CSharpReferences { get; } = ImmutableArray.Create(
            CreateAnalyzerReferenceForCompileTimeReference("Microsoft.CodeAnalysis.Features"),
            CreateAnalyzerReferenceForCompileTimeReference("Microsoft.CodeAnalysis.CSharp.Features"));

        private static AnalyzerReference CreateAnalyzerReferenceForCompileTimeReference(string assemblyName)
        {
            var assembly = Assembly.Load(assemblyName);

            return new AnalyzerFileReference(assembly.Location, new SingleAnalyzerAssemblyLoader(assembly));
        }

        private sealed class SingleAnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
        {
            private readonly Assembly assembly;

            public SingleAnalyzerAssemblyLoader(Assembly assembly)
            {
                this.assembly = assembly;
            }

            public void AddDependencyLocation(string fullPath)
            {
            }

            public Assembly LoadFromPath(string fullPath)
            {
                if (!string.Equals(fullPath, assembly.Location, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"This assembly loader must only load '{assembly.Location}'.");

                return assembly;
            }
        }
    }
}
