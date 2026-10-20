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

    public SimulatedDevicesSwitchTests()
    {
        _lighting = new LightingController([_simulated, _openRgb]);
        _switch = new SimulatedDevicesSwitch(_simulated, _lighting);
        _switch.ModeChanged += (_, _) => Interlocked.Increment(ref _modeChanges);
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

    [Fact]
    public async Task Automatic_RealDevicesCome_SimulatedOnesGoAndComeBackWhenTheRealOnesGo()
    {
        await _lighting.DiscoverAsync(Ct);

        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds() is ["real"]);

        _openRgb.SetDevices();
        await Eventually.TrueAsync(() => SimulatedCount() == 12 && DeviceIds().Count == 12);
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
    // follows the last device list and the last mode.
    [Fact]
    public async Task RealDevicesAndModesChangingAtTheSameTime_EndInTheRightState()
    {
        await _lighting.DiscoverAsync(Ct);
        SimulatedDevicesMode[] modes = [SimulatedDevicesMode.Always, SimulatedDevicesMode.Never, SimulatedDevicesMode.Automatic];

        await Task.WhenAll(
            Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    _openRgb.SetDevices(i % 2 == 0 ? [DynamicDeviceProvider.Device("real")] : []);
                }
            }, Ct),
            Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                {
                    _switch.Mode = modes[i % modes.Length];
                }
            }, Ct));

        _switch.Mode = SimulatedDevicesMode.Automatic;
        _openRgb.SetDevices(DynamicDeviceProvider.Device("real"));
        await Eventually.TrueAsync(() => DeviceIds() is ["real"]);

        _openRgb.SetDevices();
        await Eventually.TrueAsync(() => SimulatedCount() == 12 && DeviceIds().Count == 12);
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
