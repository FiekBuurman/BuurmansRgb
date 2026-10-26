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
    [InlineData(0x42000000)]
    [InlineData(0x42190000)]
    [InlineData(0x43110000)]
    public void FromExitCode_NotOfTheStep_IsAFailure(int code)
    {
        Assert.Equal(ServicesRestartReport.Failed, ServicesRestartReport.FromExitCode(code));
    }

    // The step writes no log, so the cause of its first failure comes along in the exit code.
    [Theory]
    [InlineData((int)ServicesRestartStep.StopOpenRgb, (int)ServiceControlFailure.TimedOut, 0)]
    [InlineData((int)ServicesRestartStep.StopPawnIo, (int)ServiceControlFailure.NotStoppable, 1061)]
    [InlineData((int)ServicesRestartStep.StartPawnIo, (int)ServiceControlFailure.Other, 577)]
    [InlineData((int)ServicesRestartStep.StartOpenRgb, (int)ServiceControlFailure.DependentsRunning, 1051)]
    [InlineData((int)ServicesRestartStep.Step, (int)ServiceControlFailure.Other, 5)]
    [InlineData((int)ServicesRestartStep.StartOpenRgb, (int)ServiceControlFailure.Other, 2047)]
    public void ExitCode_CarriesTheCause(int step, int failure, int win32Error)
    {
        var report = new ServicesRestartReport(ServiceOutcome.Failed, ServiceOutcome.SkippedForOpenRgb);
        var cause = new ServicesRestartCause((ServicesRestartStep)step, (ServiceControlFailure)failure, win32Error);

        var code = report.ToExitCode(cause);

        Assert.Equal(report, ServicesRestartReport.FromExitCode(code));
        Assert.Equal(cause, ServicesRestartReport.CauseFromExitCode(code));
    }

    [Fact]
    public void ExitCode_WithoutCause_HasNone()
    {
        var code = new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.Restarted).ToExitCode();

        Assert.Equal(default, ServicesRestartReport.CauseFromExitCode(code));
    }

    // A Win32 error that does not fit goes as "no error"; the rest of the cause stays.
    [Theory]
    [InlineData(2048)]
    [InlineData(-1)]
    [InlineData(unchecked((int)0x80070005))]
    public void ExitCode_Win32ErrorTooLarge_GoesAsNone(int win32Error)
    {
        var cause = new ServicesRestartCause(ServicesRestartStep.StopPawnIo, ServiceControlFailure.Other, win32Error);

        var code = ServicesRestartReport.Failed.ToExitCode(cause);

        Assert.Equal(ServicesRestartReport.Failed, ServicesRestartReport.FromExitCode(code));
        Assert.Equal(cause with { Win32Error = 0 }, ServicesRestartReport.CauseFromExitCode(code));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(unchecked((int)0xE0434352))]
    [InlineData(0x4311A041)]
    public void CauseFromExitCode_NotOfTheStep_IsNone(int code)
    {
        Assert.Equal(default, ServicesRestartReport.CauseFromExitCode(code));
    }

    [Fact]
    public void Cause_ToString_NamesTheStepAndTheError()
    {
        var text = new ServicesRestartCause(ServicesRestartStep.StopPawnIo, ServiceControlFailure.NotStoppable, 1061).ToString();

        Assert.StartsWith("stopping PawnIO: NotStoppable, Win32 error 1061 (", text, StringComparison.Ordinal);
        Assert.Equal("starting OpenRGB: TimedOut", new ServicesRestartCause(ServicesRestartStep.StartOpenRgb, ServiceControlFailure.TimedOut, 0).ToString());
    }

    [Fact]
    public void Cause_FromAnException_TakesItsFailureAndWin32Error()
    {
        var control = ServiceControl.Failure("PawnIO", 1061);
        var win32 = new System.ComponentModel.Win32Exception(5);
        var other = new IOException("Broken.");

        Assert.Equal(new ServicesRestartCause(ServicesRestartStep.StopPawnIo, ServiceControlFailure.NotStoppable, 1061), ServicesRestartCause.From(ServicesRestartStep.StopPawnIo, control));
        Assert.Equal(new ServicesRestartCause(ServicesRestartStep.Step, ServiceControlFailure.Other, 5), ServicesRestartCause.From(ServicesRestartStep.Step, win32));
        Assert.Equal(new ServicesRestartCause(ServicesRestartStep.Step, ServiceControlFailure.Other, 0), ServicesRestartCause.From(ServicesRestartStep.Step, other));
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
