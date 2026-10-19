using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerDeviceChangeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly SimulatedDeviceProvider _simulated = new();
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly LightingController _controller;

    public LightingControllerDeviceChangeTests() => _controller = new LightingController([_simulated, _dynamic]);

    [Fact]
    public async Task DevicesChanged_NewDevicesFollowTheSimulatedOnesAndGetTheLighting()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(StaticLighting(Orange)));

        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));

        Assert.Equal("real", _controller.Devices[^1].Id);
        Assert.Equal(["simulated-strip", "simulated-fan", "simulated-keyboard"], _controller.Devices.Take(3).Select(device => device.Id));
        Assert.Equal([Orange, Orange], _dynamic.Leds["real"]);
    }

    [Fact]
    public async Task DevicesChanged_RaisesRenderedWithTheNewDevice()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(StaticLighting(Orange)));
        var rendered = new TaskCompletionSource<LightingOutput>();
        _controller.Rendered += (_, _) => rendered.TrySetResult(_controller.GetOutput());

        _dynamic.SetDevices(DynamicDeviceProvider.Device("real"));

        var output = await rendered.Task.WaitAsync(Timeout, Ct);
        Assert.Equal([Orange, Orange], Assert.Single(output.Devices, device => device.DeviceId == "real").Colors);
    }

    [Fact]
    public async Task DevicesChanged_BeforeApply_SendsNothing()
    {
        await _controller.DiscoverAsync(Ct);

        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));

        Assert.Contains(_controller.Devices, device => device.Id == "real");
        Assert.Empty(_dynamic.Leds);
    }

    [Fact]
    public async Task DevicesChanged_DeviceGone_KeepsItsOwnLightingForWhenItReturns()
    {
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));
        _controller.Apply(LightingStates.WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { ["real"] = StaticLighting(Green) }));

        await ChangeDevicesAsync();
        Assert.DoesNotContain(_controller.Devices, device => device.Id == "real");
        Assert.Equal(StaticLighting(Green), _controller.State.Devices["real"]);

        _dynamic.Leds.Clear();
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));
        Assert.Equal([Green, Green], _dynamic.Leds["real"]);
    }

    [Fact]
    public async Task DevicesChanged_WhilePaused_SendsNothing()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(StaticLighting(Orange)));
        _controller.SetPaused(true);

        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));

        Assert.Empty(_dynamic.Leds);
    }

    [Fact]
    public void Dispose_StopsListeningToTheProviders()
    {
        Assert.True(_dynamic.HasDevicesChangedListeners);

        _controller.Dispose();

        Assert.False(_dynamic.HasDevicesChangedListeners);
    }

    [Fact]
    public async Task Dispose_WhileARefreshWaitsForTheProvider_LeavesTheDevicesAlone()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(StaticLighting(Orange)));
        var slowProvider = new TaskCompletionSource();
        _dynamic.DiscoverGate = slowProvider.Task;
        var devicesChanged = 0;
        _controller.DevicesChanged += (_, _) => devicesChanged++;
        _dynamic.SetDevices(DynamicDeviceProvider.Device("real"));
        await _dynamic.DiscoverStarted.Task.WaitAsync(Timeout, Ct);

        _controller.Dispose();
        // The refresh continues on this thread, so it has ended when SetResult returns.
        slowProvider.SetResult();

        Assert.Empty(_dynamic.Leds);
        Assert.Equal(0, devicesChanged);
    }

    [Fact]
    public void Dispose_Twice_IsFine()
    {
        _controller.Dispose();

        _controller.Dispose();
    }

    private async Task ChangeDevicesAsync(params IReadOnlyList<RgbDevice> devices)
    {
        var changed = new TaskCompletionSource();
        void OnChanged(object? sender, EventArgs e) => changed.TrySetResult();
        _controller.DevicesChanged += OnChanged;
        try
        {
            _dynamic.SetDevices(devices);
            await changed.Task.WaitAsync(Timeout, Ct);
        }
        finally
        {
            _controller.DevicesChanged -= OnChanged;
        }
    }
}
