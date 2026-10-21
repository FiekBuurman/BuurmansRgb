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

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--exit" }, true)]
    [InlineData(new[] { "--Exit" }, true)]
    [InlineData(new[] { "--first-run", "--exit" }, true)]
    [InlineData(new[] { "--exit-later" }, false)]
    public void Parse_FindsExitFlag(string[] args, bool exit)
    {
        Assert.Equal(exit, EngineOptions.Parse(args).Exit);
    }
}
