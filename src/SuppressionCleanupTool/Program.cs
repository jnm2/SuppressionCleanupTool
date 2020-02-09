using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuppressionCleanupTool
{
    public static partial class Program
    {
        public static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();

            using var workspace = MSBuildWorkspace.Create();

            var solutionPath = args[0];
            Console.WriteLine($"Loading {solutionPath}...");
            var originalSolution = await workspace.OpenSolutionAsync(solutionPath);

            // Enables dynamically-loaded analyzers to resolve their dependencies.
            Utils.ResolveAssembliesWithVersionRollforward(AppDomain.CurrentDomain);

            Console.WriteLine($"Searching for unnecessary suppressions...");

            var diagnosticsComparer = new SolutionDiagnosticsComparer(originalSolution);

            var newSolution = originalSolution;

            foreach (var projectId in originalSolution.ProjectIds)
            {
                var project = newSolution.GetProject(projectId);

                foreach (var documentId in project.DocumentIds)
                {
                    var document = newSolution.GetDocument(documentId);

                    var syntaxRoot = await document.GetSyntaxRootAsync();

                    var suppressions = SuppressionFixer.FindSuppressions(syntaxRoot);

                    syntaxRoot = syntaxRoot.TrackNodes(suppressions);

                    foreach (var suppressionSyntax in suppressions)
                    {
                        foreach (var removal in SuppressionFixer.GetPotentialRemovals(syntaxRoot, syntaxRoot.GetCurrentNode(suppressionSyntax)))
                        {
                            var modifiedDocument = document.WithSyntaxRoot(removal.NewRoot);

                            if (await diagnosticsComparer.HasNewCompileDiagnosticsAsync(modifiedDocument, CancellationToken.None))
                            {
                                continue;
                            }

                            if (removal.RequiredAnalyzerDiagnosticIds.Any()
                                && await diagnosticsComparer.HasNewAnalyzerDiagnosticsAsync(modifiedDocument, removal.RequiredAnalyzerDiagnosticIds, CancellationToken.None))
                            {
                                continue;
                            }

                            syntaxRoot = removal.NewRoot;
                            document = modifiedDocument;

                            var fileLineSpan = removal.RemovalLocation.GetLineSpan();
                            Console.WriteLine($"Removed '{removal.RemovedText}' from {fileLineSpan.Path} ({fileLineSpan.StartLinePosition})");
                            break;
                        }
                    }

                    newSolution = document.Project.Solution;

                    Utils.UpdateWorkspace(workspace, ref newSolution);
                }
            }

            if (newSolution == originalSolution)
                Console.WriteLine("No suppressions found that the tool could remove.");
            else
                Utils.UpdateWorkspace(workspace, ref newSolution);
        }
    }
}
