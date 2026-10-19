using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineApiTests
{
    private readonly LightingController _lighting = new([new SimulatedDeviceProvider()]);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task SetBrightnessAsync_OutOfRange_Throws(double brightness)
    {
        using var api = new EngineApi(_lighting);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => api.SetBrightnessAsync(null, brightness, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FrameRendered_ForwardsLightingFrames()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        using var api = new EngineApi(_lighting);
        LightingFrame? received = null;
        api.FrameRendered += (_, frame) => received = frame;

        await api.SetColorAsync(null, new RgbColor(1, 2, 3), TestContext.Current.CancellationToken);

        Assert.NotNull(received);
        Assert.All(received.Devices.SelectMany(device => device.ToColors()), led => Assert.Equal(new RgbColor(1, 2, 3), led));
    }

    [Fact]
    public async Task Dispose_StopsForwardingFrames()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        var api = new EngineApi(_lighting);
        var frames = 0;
        api.FrameRendered += (_, _) => frames++;

        api.Dispose();
        _lighting.Apply(LightingState.Uniform(DeviceLighting.Default));

        Assert.Equal(0, frames);
    }
}
