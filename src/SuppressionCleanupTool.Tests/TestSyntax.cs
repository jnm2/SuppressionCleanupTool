namespace SuppressionCleanupTool.Tests
{
    internal static class TestSyntax
    {
        public const string TriggeringIDE0031 = @"
class C
{
    string M(object obj) => obj is null ? null : obj.ToString();
}";

        public const string TriggeringIDE0044 = @"
class C
{
    int f = 4;
}";

        public const string TriggeringIDE0062 = @"
class C
{
    void M()
    {
        LocalFunction();
        void LocalFunction() { }
    }
}";
    }
}
