using Microsoft.CodeAnalysis;

namespace SuppressionCleanupTool.Tests
{
    internal static class TestUtils
    {
        public static Workspace CreateSingleDocumentWorkspace(string documentText, out Document document)
        {
            var workspace = new AdhocWorkspace();

            document = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .AddDocument("TestDocument.cs", documentText);

            return workspace;
        }
    }
}
