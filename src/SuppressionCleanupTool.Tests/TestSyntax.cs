using System.Collections.Generic;

namespace SuppressionCleanupTool.Tests
{
    internal static class TestSyntax
    {
        public static IReadOnlyDictionary<string, string> ByTriggeredDiagnosticId { get; } = new Dictionary<string, string>
        {
            ["IDE0031"] = @"
class C
{
    string M(object obj) => obj is null ? null : obj.ToString();
}",

            ["IDE0044"] = @"
class C
{
    int f = 4;
}",

            ["IDE0059"] = @"
class C
{
    public void M()
    {
        var i = 1;
    }
}",

            ["IDE0062"] = @"
class C
{
    void M()
    {
        LocalFunction();
        void LocalFunction() { }
    }
}",
        };
    }
}
