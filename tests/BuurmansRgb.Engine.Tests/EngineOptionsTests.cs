namespace BuurmansRgb.Engine.Tests;

public sealed class EngineOptionsTests
{
    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--first-run" }, true)]
    [InlineData(new[] { "--FIRST-RUN" }, true)]
    [InlineData(new[] { "--other", "--first-run" }, true)]
    [InlineData(new[] { "--first-run-later" }, false)]
    [InlineData(new[] { "first-run" }, false)]
    public void Parse_FindsFirstRunFlag(string[] args, bool firstRun)
    {
        Assert.Equal(firstRun, EngineOptions.Parse(args).FirstRun);
    }
}
