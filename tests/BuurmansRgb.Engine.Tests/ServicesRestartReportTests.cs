using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

public sealed class ServicesRestartReportTests
{
    // As numbers, as the outcome is internal to the engine.
    public static TheoryData<int, int> Outcomes()
    {
        var data = new TheoryData<int, int>();
        foreach (var openRgb in Enum.GetValues<ServiceOutcome>())
        {
            foreach (var pawnIo in Enum.GetValues<ServiceOutcome>())
            {
                data.Add((int)openRgb, (int)pawnIo);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void ExitCode_RoundTrips(int openRgb, int pawnIo)
    {
        var report = new ServicesRestartReport((ServiceOutcome)openRgb, (ServiceOutcome)pawnIo);

        Assert.Equal(report, ServicesRestartReport.FromExitCode(report.ToExitCode()));
        Assert.Equal(report, ServicesRestartReport.ParseArgument(report.ToArgument()));
    }

    // A crash of the step (an unhandled .NET exception, an access violation, exit code 1) or an
    // unknown outcome never reads as a result.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(unchecked((int)0xE0434352))]
    [InlineData(unchecked((int)0xC0000005))]
    [InlineData(0x42520000)]
    [InlineData(0x42520109)]
    [InlineData(0x42530101)]
    public void FromExitCode_NotOfTheStep_IsAFailure(int code)
    {
        Assert.Equal(ServicesRestartReport.Failed, ServicesRestartReport.FromExitCode(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    public void ParseArgument_Unreadable_IsNull(string? text)
    {
        Assert.Null(ServicesRestartReport.ParseArgument(text));
    }
}
