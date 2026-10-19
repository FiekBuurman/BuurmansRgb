using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerTests
{
    private const string Fan = "simulated-fan";
    private const string Strip = "simulated-strip";

    private static readonly RgbColor Orange = new(255, 136, 0);
    private static readonly RgbColor Green = new(0, 255, 0);

    private readonly SimulatedDeviceProvider _simulated = new();

    [Fact]
    public async Task Apply_FillsEveryLedOfEverySimulatedDeviceWithScaledColor()
    {
        var controller = await CreateSimulatedAsync();

        controller.Apply(Uniform(Orange, 0.5));

        var expected = Orange.Scale(0.5);
        foreach (var device in await _simulated.DiscoverAsync(TestContext.Current.CancellationToken))
        {
            var leds = _simulated.GetLeds(device.Id).ToArray();
            Assert.Equal(device.LedCount, leds.Length);
            Assert.All(leds, led => Assert.Equal(expected, led));
        }
    }

    [Fact]
    public async Task Apply_UsesEveryProvider()
    {
        var first = new RecordingDeviceProvider();
        var second = new RecordingDeviceProvider();
        var controller = new LightingController([first, second]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);

        controller.Apply(Uniform(Orange, 1.0));

        Assert.Equal([Orange, Orange, Orange], first.LastColors);
        Assert.Equal([Orange, Orange, Orange], second.LastColors);
    }

    [Fact]
    public async Task Apply_WithDeviceLighting_GivesThatDeviceItsOwnColor()
    {
        var controller = await CreateSimulatedAsync();

        controller.Apply(new LightingState(Lighting(Orange), new Dictionary<string, DeviceLighting> { [Fan] = Lighting(Green, 0.5) }));

        Assert.All(_simulated.GetLeds(Fan).ToArray(), led => Assert.Equal(Green.Scale(0.5), led));
        Assert.All(_simulated.GetLeds(Strip).ToArray(), led => Assert.Equal(Orange, led));
    }

    [Fact]
    public async Task Apply_DoesNotRaiseLightingChanged()
    {
        var controller = await CreateSimulatedAsync();
        var changes = 0;
        controller.LightingChanged += (_, _) => changes++;

        controller.Apply(Uniform(Orange, 1.0));

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Apply_WhilePaused_SendsNothing()
    {
        var provider = new RecordingDeviceProvider();
        var controller = new LightingController([provider]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);

        controller.SetPaused(true);
        controller.Apply(Uniform(Orange, 1.0));

        Assert.True(controller.IsPaused);
        Assert.Equal(0, provider.SetLedsCount);
    }

    [Fact]
    public async Task SetPausedFalse_SendsLatestColor()
    {
        var provider = new RecordingDeviceProvider();
        var controller = new LightingController([provider]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);
        controller.SetPaused(true);
        controller.Apply(Uniform(Orange, 1.0));

        controller.SetPaused(false);

        Assert.False(controller.IsPaused);
        Assert.Equal(1, provider.SetLedsCount);
        Assert.Equal([Orange, Orange, Orange], provider.LastColors);
    }

    [Fact]
    public async Task SetPausedFalse_WithoutColor_SendsNothing()
    {
        var provider = new RecordingDeviceProvider();
        var controller = new LightingController([provider]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);
        controller.SetPaused(true);

        controller.SetPaused(false);

        Assert.Equal(0, provider.SetLedsCount);
    }

    [Fact]
    public async Task SetLighting_OneDevice_ChangesOnlyThatDevice()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(Uniform(Orange, 1.0));

        controller.SetLighting(Fan, lighting => lighting with { Color = Green });

        Assert.All(_simulated.GetLeds(Fan).ToArray(), led => Assert.Equal(Green, led));
        Assert.All(_simulated.GetLeds(Strip).ToArray(), led => Assert.Equal(Orange, led));
        Assert.Equal(Lighting(Green), controller.GetLighting(Fan));
        Assert.Equal(Lighting(Orange), controller.GetLighting(Strip));
    }

    [Fact]
    public async Task SetLighting_Global_ResetsDevicesToIt()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(new LightingState(Lighting(Orange), new Dictionary<string, DeviceLighting> { [Fan] = Lighting(Green) }));

        controller.SetLighting(null, lighting => lighting with { Brightness = 0.5 });

        Assert.All(_simulated.GetLeds(Fan).ToArray(), led => Assert.Equal(Orange.Scale(0.5), led));
        Assert.Empty(controller.State.Devices);
        Assert.Equal(Lighting(Orange, 0.5), controller.State.Global);
    }

    [Fact]
    public async Task SetLighting_UnknownDevice_Throws()
    {
        var controller = await CreateSimulatedAsync();

        Assert.Throws<ArgumentException>(() => controller.SetLighting("missing", lighting => lighting));
        Assert.Throws<ArgumentException>(() => controller.GetLighting("missing"));
    }

    [Fact]
    public async Task SetLighting_RaisesLightingChanged()
    {
        var controller = await CreateSimulatedAsync();
        var changes = 0;
        controller.LightingChanged += (_, _) => changes++;

        controller.SetLighting(Fan, lighting => lighting with { Color = Green });

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task FrameRendered_CarriesRenderedColorsWithGrowingSequence()
    {
        var controller = await CreateSimulatedAsync();
        List<LightingFrame> frames = [];
        controller.FrameRendered += (_, frame) => frames.Add(frame);

        controller.Apply(Uniform(Orange, 1.0));
        controller.SetLighting(Fan, lighting => lighting with { Color = Green });

        Assert.Equal(2, frames.Count);
        Assert.True(frames[1].Sequence > frames[0].Sequence);
        var fan = Assert.Single(frames[1].Devices, device => device.DeviceId == Fan);
        Assert.All(fan.ToColors(), led => Assert.Equal(Green, led));
        Assert.Equal(_simulated.GetLeds(Fan).Length, fan.ToColors().Length);
    }

    [Fact]
    public async Task FrameRendered_WhilePaused_StillPublishesWithoutSending()
    {
        var provider = new RecordingDeviceProvider();
        var controller = new LightingController([provider]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);
        LightingFrame? frame = null;
        controller.FrameRendered += (_, rendered) => frame = rendered;
        controller.SetPaused(true);

        controller.Apply(Uniform(Orange, 1.0));

        Assert.Equal(0, provider.SetLedsCount);
        Assert.NotNull(frame);
        Assert.Equal([Orange, Orange, Orange], frame.Devices[0].ToColors());
    }

    [Fact]
    public async Task GetFrame_ReturnsLatestRenderedColors()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(Uniform(Orange, 1.0));

        var frame = controller.GetFrame();

        Assert.Equal(3, frame.Devices.Count);
        Assert.All(frame.Devices.SelectMany(device => device.ToColors()), led => Assert.Equal(Orange, led));
    }

    [Fact]
    public async Task Devices_ListsDiscoveredDevices()
    {
        var controller = await CreateSimulatedAsync();

        Assert.Equal(await _simulated.DiscoverAsync(TestContext.Current.CancellationToken), controller.Devices);
    }

    private static DeviceLighting Lighting(RgbColor color, double brightness = 1.0) => new(color, brightness, EffectIds.Static);

    private static LightingState Uniform(RgbColor color, double brightness) => LightingState.Uniform(Lighting(color, brightness));

    private async Task<LightingController> CreateSimulatedAsync()
    {
        var controller = new LightingController([_simulated]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);
        return controller;
    }
}
