using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;

namespace SuppressionCleanupTool
{
    public readonly struct SuppressionRemoval
    {
        public SyntaxNode NewRoot { get; }
        public ImmutableArray<string> RequiredAnalyzerDiagnosticIds { get; }
        public string RemovedText { get; }
        public Location RemovalLocation { get; }

        public SuppressionRemoval(SyntaxNode newRoot, ImmutableArray<string> requiredAnalyzerDiagnosticIds, string removedText, Location removalLocation)
        {
            NewRoot = newRoot ?? throw new ArgumentNullException(nameof(newRoot));
            RequiredAnalyzerDiagnosticIds = requiredAnalyzerDiagnosticIds;
            RemovedText = removedText ?? throw new ArgumentNullException(nameof(removedText));
            RemovalLocation = removalLocation ?? throw new ArgumentNullException(nameof(removalLocation));
        }
    }
}
