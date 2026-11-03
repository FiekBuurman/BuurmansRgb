using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class SimulatedDevicesSwitchTests : IDisposable
{
    private readonly SwitchableDeviceProvider _simulated = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());

    // Stands in for OpenRGB: its devices are real ones.
    private readonly DynamicDeviceProvider _openRgb = new();
    private readonly LightingController _lighting;
    private readonly SimulatedDevicesSwitch _switch;
    private int _modeChanges;
    private int _hadRealDevicesChanges;

    public SimulatedDevicesSwitchTests()
    {
        _lighting = new LightingController([_simulated, _openRgb]);
        _switch = new SimulatedDevicesSwitch(_simulated, _lighting);
        _switch.ModeChanged += (_, _) => Interlocked.Increment(ref _modeChanges);
        _switch.HadRealDevicesChanged += (_, _) => Interlocked.Increment(ref _hadRealDevicesChanges);
    }

    public void Dispose()
    {
        _switch.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public async Task Automatic_WithoutRealDevices_ShowsTheSimulatedOnes()
    {
        await _lighting.DiscoverAsync(Ct);

        Assert.Equal(SimulatedDevicesMode.Automatic, _switch.Mode);
        Assert.True(_simulated.IsEnabled);
        Assert.Equal(12, SimulatedCount());
    }

    // OpenRGB connected with real devices, then gone: this pc has real devices, so no simulated
    // ones in their place.
    [Fact]
    public async Task Automatic_RealDevicesCome_SimulatedOnesGoAndStayAwayWhenTheRealOnesGo()
    {
        await _lighting.DiscoverAsync(Ct);
        Assert.False(_switch.HadRealDevices);

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds() is ["real"]);
        Assert.True(_switch.HadRealDevices);
        Assert.Equal(1, _hadRealDevicesChanges);

        _openRgb.SetDevices();
        await Eventually.TrueAsync(() => DeviceIds().Count == 0);
        Assert.False(_simulated.IsEnabled);
        Assert.True(_switch.HadRealDevices);
        Assert.Equal(1, _hadRealDevicesChanges);
    }

    // A new engine on a pc that had real devices, while OpenRGB is not running: none either.
    [Fact]
    public async Task Automatic_HadRealDevices_KeepsTheSimulatedOnesAwayWhileOpenRgbIsGone()
    {
        var status = new FakeServerStatus();
        using var lighting = new LightingController([_simulated, _openRgb]);
        using var waiting = new SimulatedDevicesSwitch(_simulated, lighting, status) { HadRealDevices = true };
        await lighting.DiscoverAsync(Ct);

        status.Change(new ServerStatus(ServerState.Unreachable));

        Assert.False(_simulated.IsEnabled);
        await Task.Delay(100, Ct);
        Assert.Empty(lighting.Devices);
    }

    // The fact is recorded in every mode, once, so it also counts after a switch to Automatic.
    [Fact]
    public async Task HadRealDevices_IsRecordedInAnyMode()
    {
        _switch.Mode = SimulatedDevicesMode.Never;
        await _lighting.DiscoverAsync(Ct);

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => _switch.HadRealDevices);
        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"), DynamicDeviceProvider.Device("other"));
        await Eventually.TrueAsync(() => DeviceIds().Count == 2);
        _openRgb.SetDevices();
        await Eventually.TrueAsync(() => DeviceIds().Count == 0);

        _switch.Mode = SimulatedDevicesMode.Automatic;

        Assert.False(_simulated.IsEnabled);
        Assert.Equal(1, _hadRealDevicesChanges);
    }

    [Fact]
    public async Task Always_WithHadRealDevices_KeepsTheSimulatedOnes()
    {
        _switch.Mode = SimulatedDevicesMode.Always;
        await _lighting.DiscoverAsync(Ct);

        _switch.HadRealDevices = true;

        Assert.True(_simulated.IsEnabled);
        Assert.Equal(12, SimulatedCount());
    }

    // settings.json set it back by hand (or lost the line): the simulated devices come back, until
    // a real device is in the list; then it is true again and says so, so the host writes it back.
    [Fact]
    public async Task HadRealDevices_SetBackByHand_ShowsTheSimulatedOnesWhileNoRealDeviceIsThere()
    {
        await _lighting.DiscoverAsync(Ct);
        _switch.HadRealDevices = true;
        Assert.Equal(1, _hadRealDevicesChanges);
        await Eventually.TrueAsync(() => DeviceIds().Count == 0);

        _switch.HadRealDevices = false;

        Assert.Equal(2, _hadRealDevicesChanges);
        Assert.True(_simulated.IsEnabled);
        await Eventually.TrueAsync(() => SimulatedCount() == 12);

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds() is ["real"]);
        _switch.HadRealDevices = false;

        Assert.True(_switch.HadRealDevices);
        Assert.Equal(4, _hadRealDevicesChanges);
    }

    [Fact]
    public void HadRealDevices_TheSameValue_RaisesNothing()
    {
        _switch.HadRealDevices = false;
        _switch.HadRealDevices = true;
        _switch.HadRealDevices = true;

        Assert.Equal(1, _hadRealDevicesChanges);
    }

    // At the start the first try to reach OpenRGB is still on its way: no simulated lamps for that
    // second, then they come when nothing answers.
    [Fact]
    public async Task Automatic_WaitsForTheFirstTryOfOpenRgb()
    {
        var status = new FakeServerStatus();
        using var lighting = new LightingController([_simulated, _openRgb]);
        using var waiting = new SimulatedDevicesSwitch(_simulated, lighting, status);
        await lighting.DiscoverAsync(Ct);
        Assert.False(_simulated.IsEnabled);
        Assert.Empty(lighting.Devices);

        status.Change(new ServerStatus(ServerState.Unreachable));

        Assert.True(_simulated.IsEnabled);
        await Eventually.TrueAsync(() => lighting.Devices.Count == 12);
    }

    // OpenRGB has lamps, which the lighting fetches next: the simulated ones do not show meanwhile.
    [Fact]
    public async Task Automatic_OpenRgbHasDevices_WaitsForThem()
    {
        var status = new FakeServerStatus();
        using var lighting = new LightingController([_simulated, _openRgb]);
        using var waiting = new SimulatedDevicesSwitch(_simulated, lighting, status);
        await lighting.DiscoverAsync(Ct);

        status.Change(new ServerStatus(ServerState.Connected, DeviceCount: 1));
        Assert.False(_simulated.IsEnabled);

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => lighting.Devices.Select(device => device.Id).SequenceEqual(["real"]));
        Assert.False(_simulated.IsEnabled);
    }

    // After the first try, a lost connection brings them back once the real lamps are gone.
    [Fact]
    public async Task Automatic_ConnectionLostLater_ShowsThemAgain()
    {
        var status = new FakeServerStatus();
        using var lighting = new LightingController([_simulated, _openRgb]);
        using var waiting = new SimulatedDevicesSwitch(_simulated, lighting, status);
        status.Change(new ServerStatus(ServerState.Connected, DeviceCount: 0));
        await lighting.DiscoverAsync(Ct);
        Assert.True(_simulated.IsEnabled);

        status.Change(ServerStatus.Connecting);

        Assert.True(_simulated.IsEnabled);
    }

    [Fact]
    public void Dispose_StopsListeningToOpenRgb()
    {
        var status = new FakeServerStatus();
        var waiting = new SimulatedDevicesSwitch(_simulated, _lighting, status);

        waiting.Dispose();

        Assert.False(status.HasListeners);
    }

    [Fact]
    public async Task Never_HidesTheSimulatedDevicesAtOnce()
    {
        await _lighting.DiscoverAsync(Ct);

        _switch.Mode = SimulatedDevicesMode.Never;

        Assert.False(_simulated.IsEnabled);
        await Eventually.TrueAsync(() => DeviceIds().Count == 0);
    }

    [Fact]
    public async Task Always_KeepsTheSimulatedDevicesNextToTheRealOnes()
    {
        await _lighting.DiscoverAsync(Ct);
        _switch.Mode = SimulatedDevicesMode.Always;

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds().Contains("real"));

        Assert.True(_simulated.IsEnabled);
        Assert.Equal(12, SimulatedCount());
    }

    [Fact]
    public async Task BackToAutomatic_WithRealDevices_HidesTheSimulatedOnes()
    {
        await _lighting.DiscoverAsync(Ct);
        _switch.Mode = SimulatedDevicesMode.Always;
        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds().Contains("real"));

        _switch.Mode = SimulatedDevicesMode.Automatic;

        Assert.False(_simulated.IsEnabled);
        await Eventually.TrueAsync(() => DeviceIds() is ["real"]);
    }

    [Fact]
    public void Mode_BeforeTheFirstDiscover_SetsTheSwitchWithoutAFetch()
    {
        _switch.Mode = SimulatedDevicesMode.Never;

        Assert.False(_simulated.IsEnabled);
        Assert.Empty(_lighting.Devices);
    }

    [Fact]
    public void ModeChanged_IsRaisedOnlyWhenTheModeChanges()
    {
        _switch.Mode = SimulatedDevicesMode.Automatic;
        Assert.Equal(0, _modeChanges);

        _switch.Mode = SimulatedDevicesMode.Never;
        _switch.Mode = SimulatedDevicesMode.Never;
        Assert.Equal(1, _modeChanges);

        _switch.Mode = SimulatedDevicesMode.Always;
        Assert.Equal(2, _modeChanges);
    }

    [Fact]
    public void Mode_Unknown_ThrowsAndKeepsTheMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _switch.Mode = (SimulatedDevicesMode)42);

        Assert.Equal(SimulatedDevicesMode.Automatic, _switch.Mode);
        Assert.Equal(0, _modeChanges);
    }

    // OpenRGB can come and go while the user switches modes: whatever the order, the end state
    // follows the last device list and the last mode, without anything set afterwards. The real
    // device was in the list once before (the lighting may skip a list that is replaced at once,
    // so that is not left to the race), so with Automatic the end state no longer depends on the
    // last list: HadRealDevices is true already, and the simulated ones stay away either way. One
    // row for it, the one without the real device at the end.
    [Theory]
    [InlineData(SimulatedDevicesMode.Automatic, false, false)]
    [InlineData(SimulatedDevicesMode.Always, true, true)]
    [InlineData(SimulatedDevicesMode.Never, false, false)]
    public async Task RealDevicesAndModesChangingAtTheSameTime_EndInTheStateOfTheLastChanges(
        SimulatedDevicesMode lastMode, bool lastWithRealDevice, bool simulatedShown)
    {
        await _lighting.DiscoverAsync(Ct);
        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds() is ["real"]);
        Assert.True(_switch.HadRealDevices);
        SimulatedDevicesMode[] modes = [SimulatedDevicesMode.Always, SimulatedDevicesMode.Never, SimulatedDevicesMode.Automatic];
        IReadOnlyList<RgbDevice>[] deviceLists = [[DynamicDeviceProvider.Device("real")], []];

        await Task.WhenAll(
            Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    _openRgb.SetDevices(deviceLists[i % 2]);
                }

                _openRgb.SetDevices(lastWithRealDevice ? deviceLists[0] : deviceLists[1]);
            }, Ct),
            Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    _switch.Mode = modes[i % modes.Length];
                }

                _switch.Mode = lastMode;
            }, Ct));

        bool InTheEndState() =>
            _simulated.IsEnabled == simulatedShown
            && SimulatedCount() == (simulatedShown ? 12 : 0)
            && DeviceIds().Contains("real") == lastWithRealDevice;

        // Checked again a moment later, so a list that only passes by on its way does not count.
        await Eventually.TrueAsync(InTheEndState);
        await Task.Delay(200, Ct);
        Assert.True(InTheEndState());
        Assert.True(_switch.HadRealDevices);
        Assert.Equal(1, _hadRealDevicesChanges);
    }

    [Fact]
    public async Task Dispose_StopsFollowingTheDeviceList()
    {
        await _lighting.DiscoverAsync(Ct);
        _switch.Dispose();

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds().Contains("real"));

        Assert.True(_simulated.IsEnabled);
        Assert.Equal(12, SimulatedCount());
    }

    private IReadOnlyList<string> DeviceIds() => [.. _lighting.Devices.Select(device => device.Id)];

    private int SimulatedCount() => _lighting.Devices.Count(device => device.Source == DeviceSource.Simulated);
}
