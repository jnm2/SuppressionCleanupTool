using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using TaskTupleAwaiter;
using CountsDictionary = System.Collections.Immutable.ImmutableDictionary<(string Id, Microsoft.CodeAnalysis.SyntaxTree SyntaxTree), int>;

namespace SuppressionCleanupTool
{
    public sealed class SolutionWideDiagnosticsComparer
    {
        private readonly Solution baselineSolution;

        private readonly Lazy<Task<CountsDictionary>> baselineCompilerDiagnosticCounts;
        private readonly Lazy<Task<CountsDictionary>> baselineAnalyzerDiagnosticCounts;

        public SolutionWideDiagnosticsComparer(Solution baselineSolution)
        {
            this.baselineSolution = baselineSolution ?? throw new ArgumentNullException(nameof(baselineSolution));

            baselineCompilerDiagnosticCounts = new Lazy<Task<CountsDictionary>>(() =>
                GetBaselineDiagnosticCountsAsync(GetCompilerDiagnosticsAsync));

            baselineAnalyzerDiagnosticCounts = new Lazy<Task<CountsDictionary>>(() =>
                GetBaselineDiagnosticCountsAsync(GetAnalyzerDiagnosticsAsync));
        }

        private static (string Id, SyntaxTree SyntaxTree) GetDiagnosticCountKey(Diagnostic diagnostic)
        {
            return (diagnostic.Id, diagnostic.Location.SourceTree);
        }

        private async Task<CountsDictionary> GetBaselineDiagnosticCountsAsync(Func<Project, Task<ImmutableArray<Diagnostic>>> getDiagnostics)
        {
            var builder = ImmutableDictionary.CreateBuilder<(string Id, SyntaxTree SyntaxTree), int>();

            foreach (var project in baselineSolution.Projects)
            {
                var diagnostics = await getDiagnostics.Invoke(project).ConfigureAwait(false);

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

        public async Task<bool> HasNewDiagnosticsAsync(Solution updatedSolution, bool includeCompilerDiagnostics, bool includeAnalyzerDiagnostics)
        {
            var remainingCompilerCounts = (CountsDictionary.Builder)null;
            var remainingAnalyzerCounts = (CountsDictionary.Builder)null;

            foreach (var project in updatedSolution.Projects)
            {
                var (baselineCompilerDiagnosticCounts, baselineAnalyzerDiagnosticCounts, updatedCompilerDiagnostics, updatedAnalyzerDiagnostics) = await (
                    includeCompilerDiagnostics ? this.baselineCompilerDiagnosticCounts.Value : Task.FromResult(CountsDictionary.Empty),
                    includeAnalyzerDiagnostics ? this.baselineAnalyzerDiagnosticCounts.Value : Task.FromResult(CountsDictionary.Empty),

                    includeCompilerDiagnostics ? GetCompilerDiagnosticsAsync(project) : Task.FromResult(ImmutableArray<Diagnostic>.Empty),
                    includeAnalyzerDiagnostics ? GetAnalyzerDiagnosticsAsync(project) : Task.FromResult(ImmutableArray<Diagnostic>.Empty)
                ).ConfigureAwait(false);

                if (HasNewDiagnostics(baselineCompilerDiagnosticCounts, ref remainingCompilerCounts, updatedCompilerDiagnostics)
                    || HasNewDiagnostics(baselineAnalyzerDiagnosticCounts, ref remainingAnalyzerCounts, updatedAnalyzerDiagnostics))
                {
                    return true;
                }
            }

            return false;

        }

        private static bool HasNewDiagnostics(CountsDictionary baselineCounts, ref CountsDictionary.Builder remainingCounts, ImmutableArray<Diagnostic> updatedDiagnostics)
        {
            foreach (var diagnostic in updatedDiagnostics)
            {
                var key = GetDiagnosticCountKey(diagnostic);

                if (remainingCounts is null)
                {
                    if (!baselineCounts.ContainsKey(key))
                        return true;

                    remainingCounts = baselineCounts.ToBuilder();
                }

                var count = remainingCounts.GetValueOrDefault(key);
                if (count == 0) return true;

                remainingCounts[key] = count - 1;
            }

            return false;
        }

        private static async Task<ImmutableArray<Diagnostic>> GetCompilerDiagnosticsAsync(Project project)
        {
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);

            return compilation.GetDiagnostics();
        }

        private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(Project project)
        {
            var analyzers = project.AnalyzerReferences
                .SelectMany(reference => reference.GetAnalyzers(project.Language))
                .ToImmutableArray();

            if (!analyzers.Any()) return ImmutableArray<Diagnostic>.Empty;

            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);

            var compilationWithAnalyzers = compilation.WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    project.AnalyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        }
    }
}
