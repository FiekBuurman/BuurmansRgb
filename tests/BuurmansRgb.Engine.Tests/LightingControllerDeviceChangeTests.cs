using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerDeviceChangeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly RgbColor Orange = new(255, 136, 0);
    private static readonly RgbColor Green = new(0, 255, 0);

    private readonly SimulatedDeviceProvider _simulated = new();
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly LightingController _controller;

    public LightingControllerDeviceChangeTests() => _controller = new LightingController([_simulated, _dynamic]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DevicesChanged_NewDevicesFollowTheSimulatedOnesAndGetTheLighting()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Lighting(Orange)));

        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));

        Assert.Equal("real", _controller.Devices[^1].Id);
        Assert.Equal(["simulated-strip", "simulated-fan", "simulated-keyboard"], _controller.Devices.Take(3).Select(device => device.Id));
        Assert.Equal([Orange, Orange], _dynamic.Leds["real"]);
    }

    [Fact]
    public async Task DevicesChanged_RaisesRenderedWithTheNewDevice()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Lighting(Orange)));
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
        _controller.Apply(LightingStates.WithDevices(Lighting(Orange), new Dictionary<string, DeviceLighting> { ["real"] = Lighting(Green) }));

        await ChangeDevicesAsync();
        Assert.DoesNotContain(_controller.Devices, device => device.Id == "real");
        Assert.Equal(Lighting(Green), _controller.State.Devices["real"]);

        _dynamic.Leds.Clear();
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));
        Assert.Equal([Green, Green], _dynamic.Leds["real"]);
    }

    [Fact]
    public async Task DevicesChanged_WhilePaused_SendsNothing()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Lighting(Orange)));
        _controller.SetPaused(true);

        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));

        Assert.Empty(_dynamic.Leds);
    }

    private static DeviceLighting Lighting(RgbColor color) => new(color, 1.0, EffectIds.Static);

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
