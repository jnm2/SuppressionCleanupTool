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
        private readonly Lazy<Task<ImmutableHashSet<(string Id, Location location)>>> baselineDiagnostics;

        public SolutionWideDiagnosticsComparer(Solution baselineSolution)
        {
            this.baselineSolution = baselineSolution ?? throw new ArgumentNullException(nameof(baselineSolution));

            baselineDiagnostics = new Lazy<Task<ImmutableHashSet<(string Id, Location location)>>>(GetBaselineDiagnosticsAsync);
        }

        // TODO: Stop using file location because editing the file above causes existing diagnostics to show up with a
        // new location.
        private static (string Id, Location Location) GetDiagnosticKey(Diagnostic diagnostic)
        {
            return (diagnostic.Id, diagnostic.Location);
        }

        private async Task<ImmutableHashSet<(string Id, Location location)>> GetBaselineDiagnosticsAsync()
        {
            var builder = ImmutableHashSet.CreateBuilder<(string Id, Location location)>();

            foreach (var project in baselineSolution.Projects)
            {
                var diagnostics = await GetDiagnosticsAsync(project).ConfigureAwait(false);

                foreach (var diagnostic in diagnostics)
                {
                    builder.Add(GetDiagnosticKey(diagnostic));
                }
            }

            return builder.ToImmutable();
        }

        public async Task<bool> HasNewDiagnosticsAsync(Solution updatedSolution)
        {
            foreach (var project in updatedSolution.Projects)
            {
                var (baselineDiagnostics, updatedDiagnostics) = await (
                    this.baselineDiagnostics.Value,
                    GetDiagnosticsAsync(project)
                ).ConfigureAwait(false);

                foreach (var diagnostic in updatedDiagnostics)
                {
                    if (!baselineDiagnostics.Contains(GetDiagnosticKey(diagnostic)))
                        return true;
                }
            }

            return false;
        }

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(Project project)
        {
            var compilation = await project.GetCompilationAsync().ConfigureAwait(false);

            var analyzers = project.AnalyzerReferences
                .SelectMany(reference => reference.LoadAnalyzersWithVersionResolution(project.Language))
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
