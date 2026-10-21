using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerZoneTests
{
    private readonly SimulatedDeviceProvider _simulated = new();
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly LightingController _controller;

    public LightingControllerZoneTests() => _controller = new LightingController([_simulated, _dynamic]);

    [Fact]
    public async Task ResizeZone_GivesTheNewLengthTheLighting()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));

        await WhileDevicesChangeAsync(() => _controller.ResizeZone("simulated-strip", 0, 40));

        Assert.Equal(40, _controller.Devices.Single(device => device.Id == "simulated-strip").LedCount);
        var leds = _simulated.GetLeds("simulated-strip");
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
        await WhileDevicesChangeAsync(() => _dynamic.SetDevices(new RgbDevice("header", "Header", DeviceType.LedStrip, [new RgbZone("Strip", 10, 0, 60)]) { Source = DeviceSource.Hardware }));

        Assert.Throws<ArgumentException>(() => _controller.ResizeZone("header", 0, 20));
    }

    // The switch around such a provider does not claim it can resize either.
    [Fact]
    public async Task ResizeZone_SwitchableProviderThatCannotResize_Throws()
    {
        var dynamic = new DynamicDeviceProvider();
        dynamic.SetDevices(new RgbDevice("header", "Header", DeviceType.LedStrip, [new RgbZone("Strip", 10, 0, 60)]) { Source = DeviceSource.Hardware });
        using var controller = new LightingController([SwitchableDeviceProvider.For(dynamic)]);
        await controller.DiscoverAsync(Ct);

        Assert.Throws<ArgumentException>(() => controller.ResizeZone("header", 0, 20));
    }

    // As when the header is resized in OpenRGB itself: the logo after it keeps its own color.
    [Fact]
    public async Task DevicesChanged_ZoneGotAnotherLength_MovesTheLedColorsAlong()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(RgbColor.White, 1.0, EffectIds.Static)));
        await WhileDevicesChangeAsync(() => _dynamic.SetDevices(Board(header: 1)));
        _controller.SetLedColors("board", 0, [Orange, Orange, RgbColor.White, Green]);
        var lightingChanges = 0;
        _controller.LightingChanged += (_, _) => lightingChanges++;

        await WhileDevicesChangeAsync(() => _dynamic.SetDevices(Board(header: 3)));

        Assert.Equal([Orange, Orange, RgbColor.White, null, null, Green], _controller.State.LedColors["board"]);
        Assert.Equal([Orange, Orange, RgbColor.White, RgbColor.White, RgbColor.White, Green], _dynamic.Leds["board"]);
        await Eventually.TrueAsync(() => lightingChanges == 1);
    }

    private static RgbDevice Board(int header) =>
        new("board", "Board", DeviceType.Motherboard,
            [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", header, 0, 120), RgbZone.Fixed("Logo", 1)]) { Source = DeviceSource.Hardware };

    private async Task WhileDevicesChangeAsync(Action change)
    {
        var changed = new TaskCompletionSource();
        void OnChanged(object? sender, EventArgs e) => changed.TrySetResult();
        _controller.DevicesChanged += OnChanged;
        try
        {
            change();
            await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
        }
        finally
        {
            _controller.DevicesChanged -= OnChanged;
        }
    }
}
