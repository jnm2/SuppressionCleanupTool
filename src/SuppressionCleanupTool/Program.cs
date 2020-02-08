using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SuppressionCleanupTool
{
    public static partial class Program
    {
        public static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();

            using var workspace = MSBuildWorkspace.Create();

            var originalSolution = await workspace.OpenSolutionAsync(args[0]);

            // Enables dynamically-loaded analyzers to resolve their dependencies.
            Utils.ResolveAssembliesWithVersionRollforward(AppDomain.CurrentDomain);

            var diagnosticsComparer = new SolutionWideDiagnosticsComparer(originalSolution);

            var newSolution = originalSolution;

            foreach (var projectId in originalSolution.ProjectIds)
            {
                var project = newSolution.GetProject(projectId);

                foreach (var documentId in project.DocumentIds)
                {
                    var document = newSolution.GetDocument(documentId);

                    var syntaxRoot = await document.GetSyntaxRootAsync();

                    var suppressionOperators = syntaxRoot.DescendantNodes()
                        .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                        .Concat(syntaxRoot.DescendantTrivia()
                            .Where(t => t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
                            .Select(t => (PragmaWarningDirectiveTriviaSyntax)t.GetStructure())
                            .Where(s => s.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)))
                        .ToList();

                    syntaxRoot = syntaxRoot.TrackNodes(suppressionOperators);

                    foreach (var suppressionSyntax in suppressionOperators)
                    {
                        foreach (var removal in GetPotentialRemovals(syntaxRoot, syntaxRoot.GetCurrentNode(suppressionSyntax)))
                        {
                            var modifiedDocument = document.WithSyntaxRoot(removal.NewRoot);

                            if (!await diagnosticsComparer.HasNewDiagnosticsAsync(modifiedDocument.Project.Solution))
                            {
                                syntaxRoot = removal.NewRoot;
                                document = modifiedDocument;

                                var fileLineSpan = removal.RemovalLocation.GetLineSpan();
                                Console.WriteLine($"Removed '{removal.RemovedText}' from {fileLineSpan.Path} ({fileLineSpan.StartLinePosition})");
                                break;
                            }
                        }
                    }

                    newSolution = document.Project.Solution;

                    UpdateWorkspace(workspace, ref newSolution);
                }
            }

            if (newSolution == originalSolution)
                Console.WriteLine("No suppressions found that the tool could remove.");
            else
                UpdateWorkspace(workspace, ref newSolution);
        }

        private static void UpdateWorkspace(Workspace workspace, ref Solution updatedSolution)
        {
            if (!workspace.TryApplyChanges(updatedSolution))
                throw new NotImplementedException("Update failed");

            updatedSolution = workspace.CurrentSolution;
        }

        private static IEnumerable<SuppressionRemoval> GetPotentialRemovals(SyntaxNode syntaxRoot, SyntaxNode suppressionSyntax)
        {
            return suppressionSyntax.Kind() switch
            {
                SyntaxKind.SuppressNullableWarningExpression =>
                    GetPotentialRemovals(syntaxRoot, (PostfixUnaryExpressionSyntax)suppressionSyntax),

                SyntaxKind.PragmaWarningDirectiveTrivia =>
                    new[] { GetPotentialRemoval(syntaxRoot, (PragmaWarningDirectiveTriviaSyntax)suppressionSyntax) },

                _ => throw new ArgumentException("Unexpected syntax kind", nameof(suppressionSyntax)),
            };
        }

        private static IEnumerable<SuppressionRemoval> GetPotentialRemovals(SyntaxNode syntaxRoot, PostfixUnaryExpressionSyntax suppressionSyntax)
        {
            if (Facts.IsNullOrDefaultConstant(suppressionSyntax.Operand)
                && Facts.IsVariableInitializerValue(suppressionSyntax, out var variableDeclarator))
            {
                yield return new SuppressionRemoval(
                    syntaxRoot.ReplaceNode(variableDeclarator, variableDeclarator.WithInitializer(null)),
                    requiresWorkspaceDiagnostics: false,
                    variableDeclarator.Initializer.ToString(),
                    variableDeclarator.Initializer.GetLocation());
            }

            yield return new SuppressionRemoval(
                syntaxRoot.ReplaceNode(suppressionSyntax, suppressionSyntax.Operand),
                requiresWorkspaceDiagnostics: false,
                suppressionSyntax.OperatorToken.ToString(),
                suppressionSyntax.OperatorToken.GetLocation());
        }

        private static SuppressionRemoval GetPotentialRemoval(SyntaxNode syntaxRoot, PragmaWarningDirectiveTriviaSyntax suppressionSyntax)
        {
            return new SuppressionRemoval(
                syntaxRoot.RemoveNode(suppressionSyntax, SyntaxRemoveOptions.KeepExteriorTrivia),
                requiresWorkspaceDiagnostics: true,
                suppressionSyntax.ToString(),
                suppressionSyntax.GetLocation());
        }
    }
}
