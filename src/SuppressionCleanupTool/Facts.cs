using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    }
}
