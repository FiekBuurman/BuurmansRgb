using System.ComponentModel;
using System.Diagnostics;
using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The engine's side of the step with admin rights; the elevated start is a fake, so no test asks
// UAC or starts a process.
public sealed class ElevatedServicesRestartTests
{
    private const string EnginePath = @"C:\Apps\BuurmansRgb\BuurmansRgb.Engine.exe";

    private readonly FakeServiceManager _services = FakeServiceManager.WithBoth();
    private readonly List<ProcessStartInfo> _started = [];

    [Fact]
    public async Task RestartAsync_StartsTheEngineElevatedAndReadsItsExitCode()
    {
        var expected = new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.NotStopped);
        var restart = Create(_ => Task.FromResult(expected.ToExitCode()));

        var report = await restart.RestartAsync(Ct);

        Assert.Equal(expected, report);
        var startInfo = Assert.Single(_started);
        Assert.Equal(EnginePath, startInfo.FileName);
        Assert.Equal("--restart-services", startInfo.Arguments);
        Assert.Equal("runas", startInfo.Verb);
        Assert.True(startInfo.UseShellExecute);

        // The engine itself only looked; the step stops and starts.
        Assert.Empty(_services.Calls);
    }

    // The step writes no log; the engine logs the cause it gives in its exit code.
    [Fact]
    public async Task RestartAsync_StepGivesACause_LogsIt()
    {
        var report = new ServicesRestartReport(ServiceOutcome.Failed, ServiceOutcome.SkippedForOpenRgb);
        var cause = new ServicesRestartCause(ServicesRestartStep.StopOpenRgb, ServiceControlFailure.TimedOut, 0);
        var restart = Create(_ => Task.FromResult(report.ToExitCode(cause)));
        using var trace = new TraceCapture();

        Assert.Equal(report, await restart.RestartAsync(Ct));

        Assert.Contains(trace.Messages, message => message.Contains($"went wrong first at {cause}.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RestartAsync_UacDeclined_ReportsDeclined()
    {
        var restart = Create(_ => throw new Win32Exception(1223));

        Assert.Equal(ServicesRestartReport.Declined, await restart.RestartAsync(Ct));
    }

    public static TheoryData<Exception> StartFailures() =>
    [
        new Win32Exception(2),
        new InvalidOperationException("No process."),
        new TimeoutException("Too long."),
    ];

    [Theory]
    [MemberData(nameof(StartFailures))]
    public async Task RestartAsync_StepFails_ReportsFailed(Exception exception)
    {
        var restart = Create(_ => throw exception);

        Assert.Equal(ServicesRestartReport.Failed, await restart.RestartAsync(Ct));
    }

    [Fact]
    public async Task RestartAsync_StepCrashes_ReportsFailed()
    {
        var restart = Create(_ => Task.FromResult(unchecked((int)0xE0434352)));

        Assert.Equal(ServicesRestartReport.Failed, await restart.RestartAsync(Ct));
    }

    // Without OpenRGB and PawnIO there is nothing to restart, so no UAC prompt.
    [Fact]
    public async Task RestartAsync_NothingInstalled_AsksNoUac()
    {
        var restart = new ElevatedServicesRestart(EnginePath, new FakeServiceManager(), (startInfo, _, _) =>
        {
            _started.Add(startInfo);
            return Task.FromResult(0);
        });

        Assert.Equal(ServicesRestartReport.NothingInstalled, await restart.RestartAsync(Ct));
        Assert.Empty(_started);
    }

    [Fact]
    public async Task RestartAsync_OnlyPawnIo_AsksUac()
    {
        var services = new FakeServiceManager();
        services.Add("PawnIO", driver: true);
        var restart = new ElevatedServicesRestart(EnginePath, services, (startInfo, _, _) =>
        {
            _started.Add(startInfo);
            return Task.FromResult(new ServicesRestartReport(ServiceOutcome.NotInstalled, ServiceOutcome.Restarted).ToExitCode());
        });

        Assert.Equal(ServiceOutcome.Restarted, (await restart.RestartAsync(Ct)).PawnIo);
        Assert.Single(_started);
    }

    // The engine waits at least as long as the step may take.
    [Fact]
    public async Task RestartAsync_WaitsLongerThanTheStep()
    {
        var timeout = TimeSpan.Zero;
        var restart = new ElevatedServicesRestart(EnginePath, _services, (_, limit, _) =>
        {
            timeout = limit;
            return Task.FromResult(ServicesRestartReport.NothingInstalled.ToExitCode());
        });

        await restart.RestartAsync(Ct);

        Assert.True(timeout > ServiceRestarter.MaxDuration);
    }

    private ElevatedServicesRestart Create(Func<ProcessStartInfo, Task<int>> run) =>
        new(EnginePath, _services, (startInfo, _, _) =>
        {
            _started.Add(startInfo);
            return run(startInfo);
        });
}
