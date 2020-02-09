using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System;
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
                if (project is null) continue;

                foreach (var documentId in project.DocumentIds)
                {
                    var document = newSolution.GetDocument(documentId);
                    if (document is null) continue;

                    var newDocument = await SuppressionFixer.FixAllInDocumentAsync(document, diagnosticsComparer, removal =>
                    {
                        var fileLineSpan = removal.RemovalLocation.GetLineSpan();
                        Console.WriteLine($"Removed '{removal.RemovedText}' from {fileLineSpan.Path} ({fileLineSpan.StartLinePosition})");
                    }, CancellationToken.None);

                    newSolution = newDocument.Project.Solution;

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
