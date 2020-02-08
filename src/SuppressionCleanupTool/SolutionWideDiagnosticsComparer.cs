using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using TaskTupleAwaiter;

namespace SuppressionCleanupTool
{
    public sealed class SolutionWideDiagnosticsComparer
    {
        private readonly Solution baselineSolution;

        // TODO: Lazily calculate compiler diagnostics and analyzer ID-specific diagnostics separately.
        private readonly Lazy<Task<ImmutableDictionary<(string Id, SyntaxTree SyntaxTree), int>>> baselineDiagnosticCounts;

        public SolutionWideDiagnosticsComparer(Solution baselineSolution)
        {
            this.baselineSolution = baselineSolution ?? throw new ArgumentNullException(nameof(baselineSolution));

            baselineDiagnosticCounts = new Lazy<Task<ImmutableDictionary<(string Id, SyntaxTree SyntaxTree), int>>>(GetBaselineDiagnosticCountsAsync);
        }

        private static (string Id, SyntaxTree SyntaxTree) GetDiagnosticCountKey(Diagnostic diagnostic)
        {
            return (diagnostic.Id, diagnostic.Location.SourceTree);
        }

        private async Task<ImmutableDictionary<(string Id, SyntaxTree SyntaxTree), int>> GetBaselineDiagnosticCountsAsync()
        {
            var builder = ImmutableDictionary.CreateBuilder<(string Id, SyntaxTree SyntaxTree), int>();

            foreach (var project in baselineSolution.Projects)
            {
                var diagnostics = await GetDiagnosticsAsync(project).ConfigureAwait(false);

                foreach (var diagnostic in diagnostics)
                {
                    var key = GetDiagnosticCountKey(diagnostic);

                    if (builder.TryGetValue(key, out var count))
                        builder[key] = count + 1;
                    else
                        builder.Add(key, 1);
                }
            }

            return builder.ToImmutable();
        }

        public async Task<bool> HasNewDiagnosticsAsync(Solution updatedSolution)
        {
            var remainingCounts = (ImmutableDictionary<(string Id, SyntaxTree SyntaxTree), int>.Builder)null;

            foreach (var project in updatedSolution.Projects)
            {
                var (baselineDiagnosticCounts, updatedDiagnostics) = await (
                    this.baselineDiagnosticCounts.Value,
                    GetDiagnosticsAsync(project)
                ).ConfigureAwait(false);

                foreach (var diagnostic in updatedDiagnostics)
                {
                    var key = GetDiagnosticCountKey(diagnostic);

                    if (remainingCounts is null)
                    {
                        if (!baselineDiagnosticCounts.ContainsKey(key))
                            return true;

                        remainingCounts = baselineDiagnosticCounts.ToBuilder();
                    }

                    var count = remainingCounts.GetValueOrDefault(key);
                    if (count == 0) return true;

                    remainingCounts[key] = count - 1;
                }
            }

            return false;
        }

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Project project)
        {
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);

            var analyzers = project.AnalyzerReferences
                .SelectMany(reference => reference.GetAnalyzers(project.Language))
                .ToImmutableArray();

            if (!analyzers.Any())
                return compilation.GetDiagnostics();

            return await compilation
                .WithAnalyzers(
                    analyzers,
                    new CompilationWithAnalyzersOptions(
                        project.AnalyzerOptions,
                        onAnalyzerException: null,
                        concurrentAnalysis: true,
                        logAnalyzerExecutionTime: false,
                        reportSuppressedDiagnostics: false))
                .GetAllDiagnosticsAsync().ConfigureAwait(false);
        }
    }
}
