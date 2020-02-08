using Microsoft.CodeAnalysis;
using System;

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
            NewRoot = newRoot ?? throw new ArgumentNullException(nameof(newRoot));
            RequiresWorkspaceDiagnostics = requiresWorkspaceDiagnostics;
            RemovedText = removedText ?? throw new ArgumentNullException(nameof(removedText));
            RemovalLocation = removalLocation ?? throw new ArgumentNullException(nameof(removalLocation));
        }
    }
}
