using Microsoft.CodeAnalysis;

namespace SuppressionCleanupTool
{
    public readonly struct SuppressionRemoval
    {
        public SyntaxNode NewRoot { get; }
        public bool RequiresWorkspaceDiagnostics { get; }
        public string RemovedText { get; }
        public Location RemovalLocation { get; }

        public SuppressionRemoval(SyntaxNode newRoot, bool requiresWorkspaceDiagnostics, string removedText, Location removalLocation)
        {
            NewRoot = newRoot;
            RequiresWorkspaceDiagnostics = requiresWorkspaceDiagnostics;
            RemovedText = removedText;
            RemovalLocation = removalLocation;
        }
    }
}
