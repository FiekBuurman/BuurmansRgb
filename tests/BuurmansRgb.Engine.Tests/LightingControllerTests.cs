using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerTests
{
    private static readonly RgbColor Orange = new(255, 136, 0);

    [Fact]
    public async Task ApplyStaticColor_FillsEveryLedOfEverySimulatedDeviceWithScaledColor()
    {
        var simulated = new SimulatedDeviceProvider();
        var controller = new LightingController([simulated]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);

        controller.ApplyStaticColor(Orange, 0.5);

        var expected = Orange.Scale(0.5);
        foreach (var device in await simulated.DiscoverAsync(TestContext.Current.CancellationToken))
        {
            var leds = simulated.GetLeds(device.Id).ToArray();
            Assert.Equal(device.LedCount, leds.Length);
            Assert.All(leds, led => Assert.Equal(expected, led));
        }
    }

    [Fact]
    public async Task ApplyStaticColor_UsesEveryProvider()
    {
        var first = new RecordingDeviceProvider();
        var second = new RecordingDeviceProvider();
        var controller = new LightingController([first, second]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);

        controller.ApplyStaticColor(Orange, 1.0);

        Assert.Equal([Orange, Orange, Orange], first.LastColors);
        Assert.Equal([Orange, Orange, Orange], second.LastColors);
    }

    [Fact]
    public async Task ApplyStaticColor_WhilePaused_SendsNothing()
    {
        var provider = new RecordingDeviceProvider();
        var controller = new LightingController([provider]);
        await controller.DiscoverAsync(TestContext.Current.CancellationToken);

        controller.SetPaused(true);
        controller.ApplyStaticColor(Orange, 1.0);

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
        controller.ApplyStaticColor(Orange, 1.0);

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
}
