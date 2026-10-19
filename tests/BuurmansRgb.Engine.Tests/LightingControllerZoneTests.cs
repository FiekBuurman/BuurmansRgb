using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerZoneTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly SimulatedDeviceProvider _simulated = new();
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly LightingController _controller;

    public LightingControllerZoneTests() => _controller = new LightingController([_simulated, _dynamic]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResizeZone_GivesTheNewLengthTheLighting()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));

        await WhileDevicesChangeAsync(() => _controller.ResizeZone("simulated-strip", 0, 40));

        Assert.Equal(40, _controller.Devices.Single(device => device.Id == "simulated-strip").LedCount);
        var leds = _simulated.GetLeds("simulated-strip").ToArray();
        Assert.Equal(40, leds.Length);
        Assert.All(leds, led => Assert.Equal(Orange, led));
    }

    [Fact]
    public async Task ResizeZone_EmptyHeaderOfAMotherboard_GetsLeds()
    {
        await _controller.DiscoverAsync(Ct);

        await WhileDevicesChangeAsync(() => _controller.ResizeZone("simulated-motherboard", 2, 15));

        var motherboard = _controller.Devices.Single(device => device.Id == "simulated-motherboard");
        Assert.Equal(15, motherboard.Zones[2].LedCount);
        Assert.Equal(22, motherboard.LedCount);
    }

    [Fact]
    public async Task ResizeZone_FixedZone_Throws()
    {
        await _controller.DiscoverAsync(Ct);

        Assert.Throws<ArgumentException>(() => _controller.ResizeZone("simulated-fan", 0, 16));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task ResizeZone_OutsideTheRange_Throws(int ledCount)
    {
        await _controller.DiscoverAsync(Ct);

        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ResizeZone("simulated-strip", 0, ledCount));
        Assert.Equal(30, _simulated.GetLeds("simulated-strip").Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task ResizeZone_UnknownZone_Throws(int zoneIndex)
    {
        await _controller.DiscoverAsync(Ct);

        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ResizeZone("simulated-strip", zoneIndex, 20));
    }

    [Fact]
    public async Task ResizeZone_UnknownDevice_Throws()
    {
        await _controller.DiscoverAsync(Ct);

        Assert.Throws<ArgumentException>(() => _controller.ResizeZone("unknown", 0, 20));
    }

    // A provider that reports a resizable zone but cannot resize it.
    [Fact]
    public async Task ResizeZone_ProviderCannotResize_Throws()
    {
        await _controller.DiscoverAsync(Ct);
        await WhileDevicesChangeAsync(() => _dynamic.SetDevices(new RgbDevice("header", "Header", DeviceType.LedStrip, [new RgbZone("Strip", 10, 0, 60)])));

        Assert.Throws<ArgumentException>(() => _controller.ResizeZone("header", 0, 20));
    }

    private async Task WhileDevicesChangeAsync(Action change)
    {
        var changed = new TaskCompletionSource();
        void OnChanged(object? sender, EventArgs e) => changed.TrySetResult();
        _controller.DevicesChanged += OnChanged;
        try
        {
            change();
            await changed.Task.WaitAsync(Timeout, Ct);
        }
        finally
        {
            _controller.DevicesChanged -= OnChanged;
        }
    }
}
