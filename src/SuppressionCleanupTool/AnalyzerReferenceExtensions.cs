using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace SuppressionCleanupTool
{
    internal static class AnalyzerReferenceExtensions
    {
        /// <summary>
        /// Wraps <see cref="AnalyzerReference.GetAnalyzers(string)"/> so that attempts to load analyzer dependencies permit
        /// the version to roll forward.
        /// </summary>
        public static ImmutableArray<DiagnosticAnalyzer> LoadAnalyzersWithVersionResolution(this AnalyzerReference analyzerReference, string language)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAnalyzerDependency;
            try
            {
                return analyzerReference.GetAnalyzers(language);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= ResolveAnalyzerDependency;
            }
        }

        private static Assembly ResolveAnalyzerDependency(object sender, ResolveEventArgs e)
        {
            var requestedName = new AssemblyName(e.Name);
            if (requestedName.Version is { })
            {
                var anyVersion = (AssemblyName)requestedName.Clone();
                anyVersion.Version = null;

                var loaded = Assembly.Load(anyVersion);

                if (loaded.GetName().Version >= requestedName.Version)
                    return loaded;
            }

            return null;
        }
    }
}
