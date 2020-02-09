using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Shouldly;
using System.Linq;

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
    }
}
