using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Shouldly;
using SuppressionCleanupTool.Tests.Properties;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuppressionCleanupTool.Tests
{
    public static class SuppressionFixerTests
    {
        [Test]
        public static void Trivia_should_be_preserved_when_removing_nullable_warning_suppression()
        {
            RemoveSingleSuppression(@"
class C
{
    void M()
    {
        object l = null/*a*/!/*b*/;
    }
}", syntaxToRemove: "!").ShouldBe(@"
class C
{
    void M()
    {
        object l = null/*a*//*b*/;
    }
}");
        }

        [Test]
        public static async Task Multiple_enumeration_of_SyntaxNode_Descendants_should_not_be_used_because_it_triggers_Roslyn_bug()
        {
            // This file repros https://github.com/dotnet/roslyn/issues/41526.
            var documentText = Resources.AbstractAddImportFeatureService;

            await FixAllInSingleDocumentWorkspaceAsync(documentText);
        }

        private static string RemoveSingleSuppression(string compilationUnit, string syntaxToRemove)
        {
            var syntaxRoot = SyntaxFactory.ParseCompilationUnit(compilationUnit);

            var suppression = SuppressionFixer.FindSuppressions(syntaxRoot)
                .ShouldHaveSingleItem($"Expected exactly one suppression in the compilation unit.");

            var removal = SuppressionFixer.GetPotentialRemovals(syntaxRoot, suppression)
                .Where(r => r.RemovedText == syntaxToRemove)
                .ShouldHaveSingleItem($"Expected exactly one removal in the compilation unit with the removed text '{syntaxToRemove}'.");

            return removal.NewRoot.ToFullString();
        }

        private static async Task<string> FixAllInSingleDocumentWorkspaceAsync(string documentText)
        {
            using var workspace = TestUtils.CreateSingleDocumentWorkspace(documentText, out var document);

            var comparer = new SolutionDiagnosticsComparer(document.Project.Solution);

            var fixedDocument = await SuppressionFixer.FixAllInDocumentAsync(document, comparer, afterRemoval: _ => { }, CancellationToken.None);

            var fixedText = await fixedDocument.GetTextAsync();
            return fixedText.ToString();
        }
    }
}
