using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// "Starten met Windows" at the start: --first-run, which the installer gives after every install
// (also an update), turns it on unless the user chose it before (autoStartChosen). Over
// FakeAutoStart, so no test touches the Run key of this pc.
public sealed class EngineHostAutoStartTests : IDisposable
{
    private readonly FakeAutoStart _registry = new();
    private readonly AutoStartSwitch _autoStart;
    private readonly InMemorySettingsStore _store = new();
    private readonly FakeSettingsFileEvents _fileEvents = new();
    private readonly List<IDisposable> _disposables = [];

    public EngineHostAutoStartTests() => _autoStart = new AutoStartSwitch(_registry);

    public void Dispose()
    {
        foreach (var disposable in Enumerable.Reverse(_disposables))
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task FirstInstall_FirstRun_TurnsItOn()
    {
        var host = await StartAsync(firstRun: true);
        host.Dispose();

        Assert.True(_registry.IsEnabled);
        Assert.False(_store.Settings.AutoStartChosen);
        Assert.Equal(0, _store.SaveCount);
    }

    // The update after the user turned it off in the tray menu or the settings app.
    [Fact]
    public async Task TurnedOff_ThenFirstRun_StaysOff()
    {
        var installed = await StartAsync(firstRun: true);
        Assert.True(_autoStart.TrySetEnabled(false));
        installed.Dispose();
        Assert.True(_store.Settings.AutoStartChosen);
        var writes = _registry.SetEnabledCount;

        using var updated = await StartAsync(firstRun: true);

        Assert.False(_registry.IsEnabled);
        Assert.Equal(writes, _registry.SetEnabledCount);
    }

    [Fact]
    public async Task NormalStart_NotChosen_LeavesItOff()
    {
        using var host = await StartAsync(firstRun: false);

        Assert.False(_registry.IsEnabled);
        Assert.Equal(0, _registry.SetEnabledCount);
    }

    // Also a choice for the value it had already: the user saw it and left it so.
    [Fact]
    public async Task ChosenForTheSameValue_IsSaved()
    {
        var host = await StartAsync(firstRun: true);

        Assert.True(_autoStart.TrySetEnabled(true));
        host.Dispose();

        Assert.True(_store.Settings.AutoStartChosen);
    }

    // What Windows did not let change is no choice.
    [Fact]
    public async Task Refused_IsNotSaved()
    {
        var host = await StartAsync(firstRun: false);
        _registry.SetEnabledException = new UnauthorizedAccessException();

        Assert.False(_autoStart.TrySetEnabled(true));
        host.Dispose();

        Assert.False(_store.Settings.AutoStartChosen);
    }

    // settings.json from before autoStartChosen, after an install that turned autostart on: off
    // means the user turned it off, so the update leaves it off and writes that down.
    [Fact]
    public async Task FileFromBefore_Off_CountsAsChosen()
    {
        _store.Settings = new EngineSettings();

        var host = await StartAsync(firstRun: true);
        host.Dispose();

        Assert.False(_registry.IsEnabled);
        Assert.Equal(0, _registry.SetEnabledCount);
        Assert.True(_store.Settings.AutoStartChosen);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public async Task FileFromBefore_On_IsNoChoice()
    {
        _store.Settings = new EngineSettings();
        _registry.IsEnabled = true;

        var host = await StartAsync(firstRun: true);
        host.Dispose();

        Assert.True(_registry.IsEnabled);
        Assert.False(_store.Settings.AutoStartChosen);
        Assert.Equal(1, _store.SaveCount);
    }

    // Without the switch (a test, say) the file keeps what it had.
    [Fact]
    public async Task WithoutTheSwitch_FileFromBefore_StaysAsItWas()
    {
        _store.Settings = new EngineSettings();
        var host = new EngineHost(_store, Lighting(), saveDelay: TimeSpan.FromHours(1));
        await host.StartAsync(Ct);
        host.Dispose();

        Assert.Null(_store.Settings.AutoStartChosen);
        Assert.Equal(0, _store.SaveCount);
    }

    // Changed by hand, as the user may: the engine keeps it as the file has it, also when it saves
    // something else afterwards.
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task ChosenChangedByHand_IsKept(bool? byHand)
    {
        var lighting = Lighting();
        var host = await StartAsync(firstRun: false, lighting);
        Assert.True(_autoStart.TrySetEnabled(false));

        _store.ChangeByHand(new EngineSettings { AutoStartChosen = byHand, FramesPerSecond = 12 });
        _fileEvents.RaiseChanged();
        await Eventually.TrueAsync(() => lighting.FramesPerSecond == 12);
        lighting.FramesPerSecond = 20;
        host.Dispose();

        Assert.Equal(byHand, _store.Settings.AutoStartChosen);
        Assert.Equal(20, _store.Settings.FramesPerSecond);
    }

    private async Task<EngineHost> StartAsync(bool firstRun, LightingController? lighting = null)
    {
        var host = new EngineHost(
            _store,
            lighting ?? Lighting(),
            saveDelay: TimeSpan.FromHours(1),
            settingsFileEvents: _fileEvents,
            reloadDelay: TimeSpan.FromMilliseconds(10),
            autoStart: _autoStart,
            firstRun: firstRun);
        await host.StartAsync(Ct);
        return host;
    }

    private LightingController Lighting()
    {
        var lighting = new LightingController([new SimulatedDeviceProvider()]);
        _disposables.Add(lighting);
        return lighting;
    }
}
