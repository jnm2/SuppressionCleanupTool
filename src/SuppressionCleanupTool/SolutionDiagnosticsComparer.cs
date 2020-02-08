using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskTupleAwaiter;
using OccurrencesByDiagnosticId = System.Collections.Immutable.ImmutableDictionary<string, int>;

namespace SuppressionCleanupTool
{
    public sealed class SolutionDiagnosticsComparer
    {
        private readonly Solution baselineSolution;
        private readonly Dictionary<(DocumentId DocumentId, bool FromAnalyzers), Task<OccurrencesByDiagnosticId>> baselineDiagnosticCounts = new Dictionary<(DocumentId DocumentId, bool FromAnalyzers), Task<OccurrencesByDiagnosticId>>();

        public SolutionDiagnosticsComparer(Solution baselineSolution)
        {
            this.baselineSolution = baselineSolution ?? throw new ArgumentNullException(nameof(baselineSolution));
        }

        public Task<bool> HasNewCompileDiagnosticsAsync(Document updatedDocument, CancellationToken cancellationToken)
        {
            return HasNewDiagnosticsAsync(updatedDocument, fromAnalyzers: false, analyzerDiagnosticIdFilter: default, cancellationToken);
        }

        public Task<bool> HasNewAnalyzerDiagnosticsAsync(Document updatedDocument, ImmutableArray<string> diagnosticIdFilter, CancellationToken cancellationToken)
        {
            return HasNewDiagnosticsAsync(updatedDocument, fromAnalyzers: true, diagnosticIdFilter, cancellationToken);
        }

        private async Task<bool> HasNewDiagnosticsAsync(
            Document updatedDocument,
            bool fromAnalyzers,
            ImmutableArray<string>? analyzerDiagnosticIdFilter,
            CancellationToken cancellationToken)
        {
            if (fromAnalyzers && analyzerDiagnosticIdFilter is { IsDefaultOrEmpty: true })
                return false;

            var (baselineCounts, updatedDiagnostics) = await (
                GetBaselineDiagnosticsAsync(updatedDocument.Id, fromAnalyzers),
                GetDiagnosticsAsync(updatedDocument, fromAnalyzers, analyzerDiagnosticIdFilter, filterSpan: null, cancellationToken)
            ).ConfigureAwait(false);

            var remainingCounts = (OccurrencesByDiagnosticId.Builder)null;

            foreach (var diagnostic in updatedDiagnostics)
            {
                if (remainingCounts is null)
                {
                    if (!baselineCounts.ContainsKey(diagnostic.Id))
                        return true;

                    remainingCounts = baselineCounts.ToBuilder();
                }

                var count = remainingCounts.GetValueOrDefault(diagnostic.Id);
                if (count == 0) return true;

                remainingCounts[diagnostic.Id] = count - 1;
            }

            return false;
        }

        private Task<OccurrencesByDiagnosticId> GetBaselineDiagnosticsAsync(DocumentId documentId, bool fromAnalyzers)
        {
            lock (baselineDiagnosticCounts)
            {
                if (!baselineDiagnosticCounts.TryGetValue((documentId, fromAnalyzers), out var task))
                {
                    var document = baselineSolution.GetDocument(documentId);
                    task = GetOccurrencesByDiagnosticIdAsync(document, fromAnalyzers, analyzerDiagnosticIdFilter: null, CancellationToken.None);
                    baselineDiagnosticCounts.Add((documentId, fromAnalyzers), task);
                }

                return task;
            }
        }

        private async Task<OccurrencesByDiagnosticId> GetOccurrencesByDiagnosticIdAsync(
            Document document,
            bool fromAnalyzers,
            ImmutableArray<string>? analyzerDiagnosticIdFilter,
            CancellationToken cancellationToken)
        {
            var diagnostics = await GetDiagnosticsAsync(document, fromAnalyzers, analyzerDiagnosticIdFilter, filterSpan: null, cancellationToken).ConfigureAwait(false);

            return diagnostics.GroupToImmutableDictionary(
                diagnostic => diagnostic.Id,
                diagnostics => diagnostics.Count());
        }

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
            Document document,
            bool fromAnalyzers,
            ImmutableArray<string>? analyzerDiagnosticIdFilter,
            TextSpan? filterSpan,
            CancellationToken cancellationToken)
        {
            if (fromAnalyzers)
            {
                var analyzers = GetApplicableAnalyzers(document.Project, analyzerDiagnosticIdFilter);
                if (!analyzers.Any()) return ImmutableArray<Diagnostic>.Empty;

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                var compilationWithAnalyzers = semanticModel.Compilation.WithAnalyzers(
                    analyzers,
                    document.Project.AnalyzerOptions,
                    cancellationToken);

                var (syntaxDiagnostics, semanticDiagnostics) = await (
                    compilationWithAnalyzers.GetAnalyzerSyntaxDiagnosticsAsync(semanticModel.SyntaxTree, cancellationToken),
                    compilationWithAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(semanticModel, filterSpan, cancellationToken)
                ).ConfigureAwait(false);

                return syntaxDiagnostics.AddRange(semanticDiagnostics);
            }
            else
            {
                if (analyzerDiagnosticIdFilter is object)
                    throw new ArgumentException("Analyzer diagnostic ID filter must not be specified unless fromAnalyzers is true.");

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                return semanticModel.GetDiagnostics(filterSpan, cancellationToken);
            }
        }

        private static ImmutableArray<DiagnosticAnalyzer> GetApplicableAnalyzers(Project project, ImmutableArray<string>? diagnosticIdFilter)
        {
            var analyzers = project.AnalyzerReferences.SelectMany(reference => reference.GetAnalyzers(project.Language));

            if (diagnosticIdFilter is { } specifiedFilter)
            {
                analyzers = analyzers.Where(analyzer =>
                    analyzer.SupportedDiagnostics.Any(descriptor => specifiedFilter.Contains(descriptor.Id)));
            }

            return analyzers.ToImmutableArray();
        }
    }
}
