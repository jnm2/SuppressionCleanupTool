using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace SuppressionCleanupTool
{
    internal static class Utils
    {
        public static void ResolveAssembliesWithVersionRollforward(AppDomain appDomain)
        {
            appDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs e)
        {
            // Workaround for https://github.com/dotnet/runtime/issues/30997
            //                                         ↓
            var requestedName = new AssemblyName(e.Name!);
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

        public static ImmutableDictionary<TKey, TValue> GroupToImmutableDictionary<TSource, TKey, TValue>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<IEnumerable<TSource>, TValue> valueSelector,
            IEqualityComparer<TKey>? keyComparer = null)
        {
            return ImmutableDictionary.CreateRange(
                keyComparer,
                source.GroupBy(
                    keySelector,
                    (key, values) => new KeyValuePair<TKey, TValue>(key, valueSelector.Invoke(values)),
                    keyComparer));
        }

        public static void UpdateWorkspace(Workspace workspace, ref Solution updatedSolution)
        {
            if (!workspace.TryApplyChanges(updatedSolution))
                throw new NotImplementedException("Update failed");

            updatedSolution = workspace.CurrentSolution;
        }

        public static T WithAppendedTrailingTrivia<T>(this T node, SyntaxTriviaList trivia)
            where T : SyntaxNode
        {
            return trivia.Count == 0 ? node : node.WithTrailingTrivia(node.GetTrailingTrivia().Concat(trivia));
        }

        public static AnalyzerOptions? TryCreateWorkspaceAnalyzerOptions(AnalyzerOptions options, Solution solution)
        {
            if (Type.GetType("Microsoft.CodeAnalysis.Diagnostics.WorkspaceAnalyzerOptions, Microsoft.CodeAnalysis.Features") is { } workspaceAnalyzerOptionsType)
            {
                const BindingFlags allInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // Roslyn 3.5 (changes: https://github.com/dotnet/roslyn/pull/40289)
                var constructor = workspaceAnalyzerOptionsType.GetConstructor(allInstance, null, new[] { typeof(AnalyzerOptions), typeof(Solution) }, null);
                if (constructor is { })
                {
                    return (AnalyzerOptions)constructor.Invoke(new object[] { options, solution });
                }

                // Roslyn 2.3.2–3.4 (changes: https://github.com/dotnet/roslyn/pull/19277)
                constructor = workspaceAnalyzerOptionsType.GetConstructor(allInstance, null, new[] { typeof(AnalyzerOptions), typeof(OptionSet), typeof(Solution) }, null);
                if (constructor is { })
                {
                    // Solution.Options is what Microsoft.CodeAnalysis.Diagnostics.AnalyzerHelper passed.
                    return (AnalyzerOptions)constructor.Invoke(new object[] { options, solution.Options, solution });
                }

                // Roslyn 1.0–2.3.1
                constructor = workspaceAnalyzerOptionsType.GetConstructor(allInstance, null, new[] { typeof(AnalyzerOptions), typeof(Workspace) }, null);
                if (constructor is { })
                {
                    // Solution.Workspace is what Microsoft.CodeAnalysis.Diagnostics.AnalyzerHelper passed.
                    return (AnalyzerOptions)constructor.Invoke(new object[] { options, solution.Workspace });
                }
            }

            return null;
        }
    }
}
