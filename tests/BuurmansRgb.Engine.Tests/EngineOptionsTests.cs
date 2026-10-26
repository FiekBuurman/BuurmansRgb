using BuurmansRgb.Engine.Restart;

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

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--restart-services" }, true)]
    [InlineData(new[] { "--RESTART-SERVICES" }, true)]
    [InlineData(new[] { "--restarted", "1112670465" }, false)]
    public void Parse_FindsRestartServicesFlag(string[] args, bool restartServices)
    {
        Assert.Equal(restartServices, EngineOptions.Parse(args).RestartServices);
    }

    // The new engine of "Herstarten" gets the report of the old one, and whether the settings app
    // was open.
    [Fact]
    public void Parse_Restarted_ReadsTheReport()
    {
        var report = new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.NotStopped);

        var options = EngineOptions.Parse(["--restarted", report.ToArgument(), "--open-settings"]);

        Assert.Equal(report, options.Restarted);
        Assert.True(options.OpenSettings);
        Assert.False(options.RestartServices);
    }

    [Fact]
    public void Parse_WithoutRestarted_HasNoReport()
    {
        var options = EngineOptions.Parse(["--first-run"]);

        Assert.Null(options.Restarted);
        Assert.False(options.OpenSettings);
    }

    // Still a restart, so the new engine waits for the old one; what it did is not known.
    [Theory]
    [InlineData("--restarted")]
    [InlineData("--restarted --open-settings")]
    [InlineData("--restarted -5")]
    public void Parse_RestartedWithoutAReadableReport_IsAFailedRestart(string commandLine)
    {
        Assert.Equal(ServicesRestartReport.Failed, EngineOptions.Parse(commandLine.Split(' ')).Restarted);
    }
}
