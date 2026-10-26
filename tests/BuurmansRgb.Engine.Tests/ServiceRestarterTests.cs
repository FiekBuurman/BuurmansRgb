using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The step with admin rights of "Herstarten", against services in memory.
public sealed class ServiceRestarterTests
{
    // OpenRGB lets go of PawnIO before that stops, and finds it running again when it starts.
    [Fact]
    public void Restart_StopsAndStartsInOrder()
    {
        var services = FakeServiceManager.WithBoth();

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop OpenRGB", "stop PawnIO", "start PawnIO", "start OpenRGB"], services.Calls);
        Assert.Equal(new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.Restarted), report);
        Assert.Equal(["OpenRGB", "PawnIO"], services.Running.Order());
    }

    // A driver another program holds, or one that cannot unload: skipped, and OpenRGB goes on.
    [Theory]
    [InlineData((int)ServiceControlFailure.NotStoppable)]
    [InlineData((int)ServiceControlFailure.TimedOut)]
    [InlineData((int)ServiceControlFailure.DependentsRunning)]
    public void Restart_PawnIoDoesNotStop_SkipsItAndRestartsOpenRgb(int failure)
    {
        var services = FakeServiceManager.WithBoth();
        services.StopFailures["PawnIO"] = (ServiceControlFailure)failure;

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop OpenRGB", "stop PawnIO", "start OpenRGB"], services.Calls);
        Assert.Equal(new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.NotStopped), report);
    }

    [Fact]
    public void Restart_WithoutPawnIo_RestartsOnlyOpenRgb()
    {
        var services = new FakeServiceManager();
        services.Add("OpenRGB");

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop OpenRGB", "start OpenRGB"], services.Calls);
        Assert.Equal(new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.NotInstalled), report);
    }

    // The name of the driver is not certain: a driver with PawnIO in its name counts too.
    [Fact]
    public void Restart_PawnIoUnderAnotherName_FindsTheDriver()
    {
        var services = new FakeServiceManager();
        services.Add("OpenRGB");
        services.Add("pawnio_2", driver: true);

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop OpenRGB", "stop pawnio_2", "start pawnio_2", "start OpenRGB"], services.Calls);
        Assert.Equal(ServiceOutcome.Restarted, report.PawnIo);
    }

    [Fact]
    public void Restart_WithoutOpenRgb_RestartsPawnIo()
    {
        var services = new FakeServiceManager();
        services.Add("PawnIO", driver: true);

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop PawnIO", "start PawnIO"], services.Calls);
        Assert.Equal(new ServicesRestartReport(ServiceOutcome.NotInstalled, ServiceOutcome.Restarted), report);
    }

    // OpenRGB that still runs holds PawnIO, so that stays; OpenRGB is started anyway, so it runs.
    [Fact]
    public void Restart_OpenRgbDoesNotStop_LeavesPawnIoAndReportsAFailure()
    {
        var services = FakeServiceManager.WithBoth();
        services.StopFailures["OpenRGB"] = ServiceControlFailure.TimedOut;

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop OpenRGB", "start OpenRGB"], services.Calls);
        Assert.Equal(new ServicesRestartReport(ServiceOutcome.Failed, ServiceOutcome.SkippedForOpenRgb), report);
    }

    // PawnIO that does not start again: OpenRGB still starts, without the devices that need it.
    [Fact]
    public void Restart_PawnIoDoesNotStartAgain_StillStartsOpenRgb()
    {
        var services = FakeServiceManager.WithBoth();
        services.StartFailures.Add("PawnIO");

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(["stop OpenRGB", "stop PawnIO", "start PawnIO", "start OpenRGB"], services.Calls);
        Assert.Equal(new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.Failed), report);
    }

    [Fact]
    public void Restart_OpenRgbDoesNotStartAgain_ReportsAFailure()
    {
        var services = FakeServiceManager.WithBoth();
        services.StartFailures.Add("OpenRGB");

        var report = new ServiceRestarter(services).Restart();

        Assert.Equal(new ServicesRestartReport(ServiceOutcome.Failed, ServiceOutcome.Restarted), report);
    }

    [Fact]
    public void Restart_NothingInstalled_DoesNothing()
    {
        var services = new FakeServiceManager();

        var report = new ServiceRestarter(services).Restart();

        Assert.Empty(services.Calls);
        Assert.Equal(ServicesRestartReport.NothingInstalled, report);
    }

    [Fact]
    public void FindPawnIo_PrefersTheExactName()
    {
        var services = new FakeServiceManager();
        services.Add("PawnIOHelper", driver: true);
        services.Add("PawnIO", driver: true);

        Assert.Equal("PawnIO", ServiceRestarter.FindPawnIo(services));
    }
}
