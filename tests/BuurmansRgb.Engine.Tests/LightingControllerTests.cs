using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerTests
{
    private const string Fan = "simulated-fan";
    private const string Strip = "simulated-strip";

    private readonly SimulatedDeviceProvider _simulated = new();

    [Fact]
    public async Task Apply_FillsEveryLedOfEverySimulatedDeviceWithScaledColor()
    {
        var controller = await CreateSimulatedAsync();

        controller.Apply(Uniform(Orange, 0.5));

        var expected = Orange.Scale(0.5);
        foreach (var device in await _simulated.DiscoverAsync(Ct))
        {
            var leds = _simulated.GetLeds(device.Id);
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
        await controller.DiscoverAsync(Ct);

        controller.Apply(Uniform(Orange, 1.0));

        Assert.Equal([Orange, Orange, Orange], first.LastColors);
        Assert.Equal([Orange, Orange, Orange], second.LastColors);
    }

    [Fact]
    public async Task Apply_WithDeviceLighting_GivesThatDeviceItsOwnColor()
    {
        var controller = await CreateSimulatedAsync();

        controller.Apply(LightingStates.WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { [Fan] = StaticLighting(Green, 0.5) }));

        Assert.All(_simulated.GetLeds(Fan), led => Assert.Equal(Green.Scale(0.5), led));
        Assert.All(_simulated.GetLeds(Strip), led => Assert.Equal(Orange, led));
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
        await controller.DiscoverAsync(Ct);

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
        await controller.DiscoverAsync(Ct);
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
        await controller.DiscoverAsync(Ct);
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

        Assert.All(_simulated.GetLeds(Fan), led => Assert.Equal(Green, led));
        Assert.All(_simulated.GetLeds(Strip), led => Assert.Equal(Orange, led));
        Assert.Equal(StaticLighting(Green), controller.GetLighting(Fan));
        Assert.Equal(StaticLighting(Orange), controller.GetLighting(Strip));
    }

    [Fact]
    public async Task SetLighting_GlobalBrightness_KeepsDeviceColor()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(LightingStates.WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { [Fan] = StaticLighting(Green) }));

        controller.SetLighting(null, lighting => lighting with { Brightness = 0.5 });

        Assert.All(_simulated.GetLeds(Fan), led => Assert.Equal(Green.Scale(0.5), led));
        Assert.All(_simulated.GetLeds(Strip), led => Assert.Equal(Orange.Scale(0.5), led));
        Assert.Equal(StaticLighting(Orange, 0.5), controller.State.Global);
        Assert.Equal(StaticLighting(Green, 0.5), controller.State.Devices[Fan]);
    }

    [Fact]
    public async Task SetLighting_GlobalColor_KeepsDeviceBrightness()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(LightingStates.WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { [Fan] = StaticLighting(Orange, 0.5) }));

        controller.SetLighting(null, lighting => lighting with { Color = Green });

        Assert.All(_simulated.GetLeds(Fan), led => Assert.Equal(Green.Scale(0.5), led));
        Assert.Equal(StaticLighting(Green, 0.5), controller.State.Devices[Fan]);
    }

    [Fact]
    public async Task SetLighting_GlobalMatchesDevice_DropsItsOwnLighting()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(LightingStates.WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { [Fan] = StaticLighting(Green) }));

        controller.SetLighting(null, lighting => lighting with { Color = Green });

        Assert.Empty(controller.State.Devices);
        Assert.Equal(StaticLighting(Green), controller.State.Global);
    }

    [Fact]
    public async Task SetLighting_DeviceBackToGlobal_DropsItsOwnLighting()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(Uniform(Orange, 1.0));
        controller.SetLighting(Fan, lighting => lighting with { Color = Green });

        controller.SetLighting(Fan, lighting => lighting with { Color = Orange });

        Assert.Empty(controller.State.Devices);
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
    public async Task Rendered_OutputHasRenderedColorsWithGrowingSequence()
    {
        var controller = await CreateSimulatedAsync();
        List<LightingOutput> outputs = [];
        controller.Rendered += (_, _) => outputs.Add(controller.GetOutput());

        controller.Apply(Uniform(Orange, 1.0));
        controller.SetLighting(Fan, lighting => lighting with { Color = Green });

        Assert.Equal(2, outputs.Count);
        Assert.True(outputs[1].Sequence > outputs[0].Sequence);
        var fan = Assert.Single(outputs[1].Devices, device => device.DeviceId == Fan);
        Assert.All(fan.Colors, led => Assert.Equal(Green, led));
        Assert.Equal(_simulated.GetLeds(Fan).Length, fan.Colors.Length);
    }

    [Fact]
    public async Task Rendered_WhilePaused_StillRaisedWithoutSending()
    {
        var provider = new RecordingDeviceProvider();
        var controller = new LightingController([provider]);
        await controller.DiscoverAsync(Ct);
        LightingOutput? output = null;
        controller.Rendered += (_, _) => output = controller.GetOutput();
        controller.SetPaused(true);

        controller.Apply(Uniform(Orange, 1.0));

        Assert.Equal(0, provider.SetLedsCount);
        Assert.NotNull(output);
        Assert.Equal([Orange, Orange, Orange], output.Devices[0].Colors);
    }

    [Fact]
    public async Task GetOutput_ReturnsCopyOfLatestRenderedColors()
    {
        var controller = await CreateSimulatedAsync();
        controller.Apply(Uniform(Orange, 1.0));

        var output = controller.GetOutput();
        controller.SetLighting(null, lighting => lighting with { Color = Green });

        Assert.Equal(controller.Devices.Count, output.Devices.Count);
        Assert.All(output.Devices.SelectMany(device => device.Colors), led => Assert.Equal(Orange, led));
    }

    [Fact]
    public async Task Devices_ListsDiscoveredDevices()
    {
        var controller = await CreateSimulatedAsync();

        Assert.Equal(await _simulated.DiscoverAsync(Ct), controller.Devices);
    }

    private static LightingState Uniform(RgbColor color, double brightness) => LightingStates.Uniform(StaticLighting(color, brightness));

    private async Task<LightingController> CreateSimulatedAsync()
    {
        var controller = new LightingController([_simulated]);
        await controller.DiscoverAsync(Ct);
        return controller;
    }
}
