using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace SuppressionCleanupTool.Tests
{
    public static class SolutionDiagnosticsComparerTests
    {
        [TestCase("IDE0031")]
        [TestCase("IDE0044")]
        [TestCase("IDE0062")]
        public static async Task Document_options_are_provided_so_that_analyzers_return_diagnostics_as_normal(string diagnosticId)
        {
            var syntaxTriggeringDiagnostic = TestSyntax.ByTriggeredDiagnosticId[diagnosticId];

            var syntaxTriggeringDiagnosticWithPragma = "#pragma warning disable " + diagnosticId + Environment.NewLine + syntaxTriggeringDiagnostic;

            var newDiagnostics = await HasNewAnalyzerDiagnosticsAsync(
                originalDocument: syntaxTriggeringDiagnosticWithPragma,
                updatedDocument: syntaxTriggeringDiagnostic,
                diagnosticId);

            if (!newDiagnostics)
            {
                Assert.Fail($"Expected {diagnosticId} to be reported at least once in:" + Environment.NewLine + syntaxTriggeringDiagnostic);
            }
        }

        private static async Task<bool> HasNewAnalyzerDiagnosticsAsync(string originalDocument, string updatedDocument, string diagnosticId)
        {
            using var workspace = new AdhocWorkspace();

            workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create()));

            var document = workspace.AddProject("TestProject", LanguageNames.CSharp)
                .AddDocument("TestDocument.cs", originalDocument);

            var comparer = new SolutionDiagnosticsComparer(document.Project.Solution);

            return await comparer.HasNewAnalyzerDiagnosticsAsync(document.WithText(
                SourceText.From(updatedDocument)),
                diagnosticIdFilter: ImmutableArray.Create(diagnosticId),
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}
