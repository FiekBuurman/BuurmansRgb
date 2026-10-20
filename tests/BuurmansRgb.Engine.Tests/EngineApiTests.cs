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
    public async Task GetDevicesAsync_GivesTypeAndZonesOfEveryDevice()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        using var api = new EngineApi(_lighting);

        var devices = await api.GetDevicesAsync(TestContext.Current.CancellationToken);

        var motherboard = devices.Single(device => device.Id == "simulated-motherboard");
        Assert.Equal(DeviceType.Motherboard, motherboard.Type);
        Assert.Equal(7, motherboard.LedCount);
        Assert.Equal(["Mainboard", "RGB header", "ARGB header"], motherboard.Zones.Select(zone => zone.Name));
    }

    [Fact]
    public async Task SetLedColorsAsync_StoresTheColorsOfTheDevice()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        using var api = new EngineApi(_lighting);

        await api.SetLedColorsAsync("simulated-fan", 1, [new RgbColor(1, 2, 3)], TestContext.Current.CancellationToken);

        var colors = await api.GetLedColorsAsync("simulated-fan", TestContext.Current.CancellationToken);
        Assert.Equal(12, colors.Count);
        Assert.Equal(new RgbColor(1, 2, 3), colors[1]);
    }

    [Fact]
    public async Task SetLedColorsAsync_NoColors_Throws()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        using var api = new EngineApi(_lighting);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => api.SetLedColorsAsync("simulated-fan", 0, null!, TestContext.Current.CancellationToken));
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
    public async Task DevicesChanged_IsForwardedWithTheNewDeviceList()
    {
        var dynamic = new DynamicDeviceProvider();
        var lighting = new LightingController([dynamic]);
        using var api = new EngineApi(lighting);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DevicesChanged += (_, _) => changed.TrySetResult();

        dynamic.SetDevices(DynamicDeviceProvider.Device("real"));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var device = Assert.Single(await api.GetDevicesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("real", device.Id);
    }

    [Fact]
    public async Task Dispose_StopsForwardingFrames()
    {
        await _lighting.DiscoverAsync(TestContext.Current.CancellationToken);
        var api = new EngineApi(_lighting);
        var frames = 0;
        api.FrameRendered += (_, _) => frames++;

        api.Dispose();
        _lighting.Apply(LightingStates.Uniform(DeviceLighting.Default));

        Assert.Equal(0, frames);
    }
}
