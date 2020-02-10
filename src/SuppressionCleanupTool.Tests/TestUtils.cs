using Microsoft.CodeAnalysis;

namespace SuppressionCleanupTool.Tests
{
    internal static class TestUtils
    {
        public static Workspace CreateSingleDocumentWorkspace(string documentText, out Document document)
        {
            var workspace = new AdhocWorkspace();

            var project = workspace
                .AddProject("TestProject", LanguageNames.CSharp)
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            document = project.AddDocument("TestDocument.cs", documentText);

            return workspace;
        }
    }
}
