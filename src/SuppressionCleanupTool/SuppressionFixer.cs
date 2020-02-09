﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SuppressionCleanupTool
{
    public static class SuppressionFixer
    {
        public static IEnumerable<SyntaxNode> FindSuppressions(SyntaxNode syntaxRoot)
        {
            var nullabilitySuppressions = syntaxRoot.DescendantNodes()
                .Where(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression));

            var pragmaSuppressions = syntaxRoot.DescendantTrivia()
                .Where(t => t.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
                .Select(t => (PragmaWarningDirectiveTriviaSyntax)t.GetStructure())
                .Where(s => s.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword));

            return nullabilitySuppressions.Concat(pragmaSuppressions);
        }

        public static IEnumerable<SuppressionRemoval> GetPotentialRemovals(SyntaxNode syntaxRoot, SyntaxNode suppressionSyntax)
        {
            if (!syntaxRoot.Contains(suppressionSyntax))
                throw new ArgumentException("The specified suppression syntax is not contained in the specified syntax root.");

            return suppressionSyntax.Kind() switch
            {
                SyntaxKind.SuppressNullableWarningExpression =>
                    GetPotentialRemovals(syntaxRoot, (PostfixUnaryExpressionSyntax)suppressionSyntax),

                SyntaxKind.PragmaWarningDirectiveTrivia =>
                    new[] { GetPotentialRemoval(syntaxRoot, (PragmaWarningDirectiveTriviaSyntax)suppressionSyntax) },

                _ => Enumerable.Empty<SuppressionRemoval>(),
            };
        }

        private static IEnumerable<SuppressionRemoval> GetPotentialRemovals(SyntaxNode syntaxRoot, PostfixUnaryExpressionSyntax suppressionSyntax)
        {
            if (Facts.IsNullOrDefaultConstant(suppressionSyntax.Operand)
                && Facts.IsVariableInitializerValue(suppressionSyntax, out var variableDeclarator))
            {
                yield return new SuppressionRemoval(
                    syntaxRoot.ReplaceNode(variableDeclarator, variableDeclarator.WithInitializer(null)),
                    requiredAnalyzerDiagnosticIds: ImmutableArray<string>.Empty,
                    variableDeclarator.Initializer.ToString(),
                    variableDeclarator.Initializer.GetLocation());
            }

            yield return new SuppressionRemoval(
                syntaxRoot.ReplaceNode(suppressionSyntax, suppressionSyntax.Operand.WithAppendedTrailingTrivia(suppressionSyntax.GetTrailingTrivia())),
                requiredAnalyzerDiagnosticIds: ImmutableArray<string>.Empty,
                suppressionSyntax.OperatorToken.ToString(),
                suppressionSyntax.OperatorToken.GetLocation());
        }

        private static SuppressionRemoval GetPotentialRemoval(SyntaxNode syntaxRoot, PragmaWarningDirectiveTriviaSyntax suppressionSyntax)
        {
            if (suppressionSyntax.ErrorCodes.Count != 1)
                throw new NotImplementedException("TODO: remove error codes one at a time");

            var diagnosticId = Facts.GetPragmaErrorCode(suppressionSyntax.ErrorCodes[0]);

            var matchingRestorePragma = Facts.FindPragmaWarningRestore(
                syntaxRoot,
                startPosition: suppressionSyntax.Span.End,
                errorCode: diagnosticId);

            var nodesToRemove = matchingRestorePragma is { }
                ? new[] { suppressionSyntax, matchingRestorePragma }
                : new[] { suppressionSyntax };

            return new SuppressionRemoval(
                syntaxRoot.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia),
                requiredAnalyzerDiagnosticIds: ImmutableArray.Create(diagnosticId),
                suppressionSyntax.ToString(),
                suppressionSyntax.GetLocation());
        }
    }
}