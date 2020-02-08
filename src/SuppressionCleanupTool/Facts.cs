using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace SuppressionCleanupTool
{
    internal static class Facts
    {
        public static bool IsNullOrDefaultConstant(ExpressionSyntax node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.NullLiteralExpression:
                case SyntaxKind.DefaultLiteralExpression:
                case SyntaxKind.DefaultExpression:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsVariableInitializerValue(SyntaxNode syntaxNode, out VariableDeclaratorSyntax variableDeclarator)
        {
            variableDeclarator = (syntaxNode.Parent as EqualsValueClauseSyntax)?.Parent as VariableDeclaratorSyntax;

            return variableDeclarator is { };
        }

        public static PragmaWarningDirectiveTriviaSyntax FindPragmaWarningRestore(SyntaxNode syntaxRoot, int startPosition, string errorCode)
        {
            return syntaxRoot.DescendantTrivia()
                .Where(trivia => trivia.SpanStart >= startPosition && trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
                .Select(trivia => (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure())
                .FirstOrDefault(pragma =>
                    pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword)
                    && pragma.ErrorCodes.Any(code => GetPragmaErrorCode(code) == errorCode));
        }

        public static string GetPragmaErrorCode(ExpressionSyntax pragmaErrorCodeSyntax)
        {
            return pragmaErrorCodeSyntax switch
            {
                LiteralExpressionSyntax syntax => $"CS{(int)syntax.Token.Value:0000}",
                IdentifierNameSyntax syntax => syntax.Identifier.ValueText,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
