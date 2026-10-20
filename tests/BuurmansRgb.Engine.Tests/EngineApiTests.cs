using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
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
            () => api.SetBrightnessAsync(null, brightness, Ct));
    }

    [Fact]
    public async Task SetSpeedAsync_SetsTheSpeedOfTheDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);

        await api.SetSpeedAsync("simulated-fan", 2.5, Ct);

        Assert.Equal(2.5, (await api.GetLightingAsync("simulated-fan", Ct)).Speed);
        Assert.Equal(EffectSpeed.Default, (await api.GetLightingAsync(null, Ct)).Speed);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(EffectSpeed.Min - 0.01)]
    [InlineData(EffectSpeed.Max + 0.01)]
    public async Task SetSpeedAsync_OutOfRange_Throws(double speed)
    {
        using var api = new EngineApi(_lighting);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.SetSpeedAsync(null, speed, Ct));
    }

    [Fact]
    public void SettingsSaved_FollowsTheSaveEvents()
    {
        var saveEvents = new SettingsSaveEvents();
        using var api = new EngineApi(_lighting, saveEvents);
        var results = new List<SettingsSaveResult>();
        api.SettingsSaved += (_, result) => results.Add(result);

        saveEvents.Raise(succeeded: true);
        saveEvents.Raise(succeeded: false);

        Assert.Equal([new SettingsSaveResult(true), new SettingsSaveResult(false)], results);
    }

    [Fact]
    public void Dispose_StopsFollowingTheSaveEvents()
    {
        var saveEvents = new SettingsSaveEvents();
        var api = new EngineApi(_lighting, saveEvents);
        var results = 0;
        api.SettingsSaved += (_, _) => results++;

        api.Dispose();
        saveEvents.Raise(succeeded: true);

        Assert.Equal(0, results);
    }

    [Fact]
    public async Task GetDevicesAsync_GivesTypeAndZonesOfEveryDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);

        var devices = await api.GetDevicesAsync(Ct);

        var motherboard = devices.Single(device => device.Id == "simulated-motherboard");
        Assert.Equal(DeviceType.Motherboard, motherboard.Type);
        Assert.Equal(7, motherboard.LedCount);
        Assert.Equal(["Mainboard", "RGB header", "ARGB header"], motherboard.Zones.Select(zone => zone.Name));
    }

    [Fact]
    public async Task SetLedColorsAsync_StoresTheColorsOfTheDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);

        await api.SetLedColorsAsync("simulated-fan", 1, [new RgbColor(1, 2, 3)], Ct);

        var colors = await api.GetLedColorsAsync("simulated-fan", Ct);
        Assert.Equal(12, colors.Count);
        Assert.Equal(new RgbColor(1, 2, 3), colors[1]);
    }

    [Fact]
    public async Task SetLedColorsAsync_NoColors_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => api.SetLedColorsAsync("simulated-fan", 0, null!, Ct));
    }

    [Fact]
    public async Task ResizeZoneAsync_ResizesTheZoneAndReportsTheNewDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DevicesChanged += (_, _) => changed.TrySetResult();

        await api.ResizeZoneAsync("simulated-strip", 0, 45, Ct);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var devices = await api.GetDevicesAsync(Ct);
        var strip = devices.Single(device => device.Id == "simulated-strip");
        Assert.Equal(45, strip.LedCount);
        Assert.Equal(new RgbZone("Strip", 45, 1, 60), Assert.Single(strip.Zones));
    }

    [Fact]
    public async Task ResizeZoneAsync_FixedZone_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);

        await Assert.ThrowsAsync<ArgumentException>(
            () => api.ResizeZoneAsync("simulated-fan", 0, 16, Ct));
    }

    [Fact]
    public async Task FrameRendered_ForwardsLightingFrames()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting);
        LightingFrame? received = null;
        api.FrameRendered += (_, frame) => received = frame;

        await api.SetColorAsync(null, new RgbColor(1, 2, 3), Ct);

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
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var device = Assert.Single(await api.GetDevicesAsync(Ct));
        Assert.Equal("real", device.Id);
    }

    [Fact]
    public async Task Dispose_StopsForwardingFrames()
    {
        await _lighting.DiscoverAsync(Ct);
        var api = new EngineApi(_lighting);
        var frames = 0;
        api.FrameRendered += (_, _) => frames++;

        api.Dispose();
        _lighting.Apply(LightingStates.Uniform(DeviceLighting.Default));

        Assert.Equal(0, frames);
    }
}
