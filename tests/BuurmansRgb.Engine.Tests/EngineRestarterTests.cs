using BuurmansRgb.Engine.Restart;
using BuurmansRgb.Platform;

namespace BuurmansRgb.Engine.Tests;

// "Herstarten" with fakes for the services, the settings app and the new engine.
public sealed class EngineRestarterTests
{
    private readonly FakeServicesRestart _services = new();
    private readonly FakeSettingsAppWindow _settingsApp = new();
    private readonly FakeEngineLauncher _launcher = new();

    [Fact]
    public async Task RestartAsync_StartsANewEngineWithTheReport()
    {
        _services.Report = new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.NotStopped);

        var result = await CreateRestarter().RestartAsync(Ct);

        Assert.True(result.NewEngineStarted);
        Assert.False(result.SettingsAppClosed);
        Assert.Equal(["--restarted", _services.Report.ToArgument()], Assert.Single(_launcher.Starts));
        Assert.Equal(_services.Report, EngineOptions.Parse(_launcher.Starts[0]).Restarted);
    }

    // The new engine opens the settings app again; the services restart before it closes.
    [Fact]
    public async Task RestartAsync_SettingsAppOpen_ClosesItAndTheNewEngineOpensIt()
    {
        _settingsApp.IsOpen = true;
        _services.OnRestart = () => Assert.True(_settingsApp.IsOpen);

        var result = await CreateRestarter().RestartAsync(Ct);

        Assert.True(result.SettingsAppClosed);
        Assert.False(_settingsApp.IsOpen);
        var options = EngineOptions.Parse(Assert.Single(_launcher.Starts));
        Assert.True(options.OpenSettings);
    }

    // No to UAC: the engine still restarts, and tells the new one so.
    [Fact]
    public async Task RestartAsync_UacDeclined_StillRestartsTheEngine()
    {
        _services.Report = ServicesRestartReport.Declined;

        var result = await CreateRestarter().RestartAsync(Ct);

        Assert.True(result.NewEngineStarted);
        Assert.Equal(ServicesRestartReport.Declined, EngineOptions.Parse(Assert.Single(_launcher.Starts)).Restarted);
    }

    [Fact]
    public async Task RestartAsync_NewEngineDoesNotStart_SaysSo()
    {
        _launcher.Result = false;
        _settingsApp.IsOpen = true;

        var result = await CreateRestarter().RestartAsync(Ct);

        Assert.False(result.NewEngineStarted);
        Assert.True(result.SettingsAppClosed);
    }

    // The real settings app window, through a mutex of its own name.
    [Fact]
    public async Task SettingsAppWindow_Open_AsksItToExit()
    {
        var name = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
        using var settingsApp = SingleInstance.Acquire(name);
        var exited = new TaskCompletionSource();
        using var exit = settingsApp.ListenForExit(() => exited.TrySetResult());

        Assert.True(new SettingsAppWindow(name).CloseIfOpen());

        await exited.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public void SettingsAppWindow_Closed_ReturnsFalse()
    {
        Assert.False(new SettingsAppWindow($"BuurmansRgb.Tests.{Guid.NewGuid():N}").CloseIfOpen());
    }

    private EngineRestarter CreateRestarter() => new(_services, _settingsApp, _launcher);
}
