using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineApiTests
{
    private readonly SwitchableDeviceProvider _simulated = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
    private readonly LightingController _lighting;
    private readonly InMemoryProfileStore _profileStore = new();
    private readonly ProfileLibrary _profiles;
    private readonly SimulatedDevicesSwitch _simulatedDevices;

    public EngineApiTests()
    {
        _lighting = new LightingController([_simulated]);
        _profiles = new ProfileLibrary(_profileStore, _lighting);
        _simulatedDevices = new SimulatedDevicesSwitch(_simulated, _lighting);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task SetBrightnessAsync_OutOfRange_Throws(double brightness)
    {
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => api.SetBrightnessAsync(null, brightness, Ct));
    }

    [Fact]
    public async Task SetSpeedAsync_SetsTheSpeedOfTheDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

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
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.SetSpeedAsync(null, speed, Ct));
    }

    [Fact]
    public async Task SetColorLoopAsync_StoresACopyOfTheColors()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
        RgbColor[] colors = [new(255, 255, 0), new(0, 255, 0), new(0, 0, 255)];

        await api.SetColorLoopAsync(null, colors, spread: false, Ct);
        colors[0] = new RgbColor(1, 1, 1);

        var loop = (await api.GetLightingAsync(null, Ct)).Loop;
        Assert.Equal([new RgbColor(255, 255, 0), new RgbColor(0, 255, 0), new RgbColor(0, 0, 255)], loop!.Colors);
        Assert.False(loop.Spread);
    }

    [Fact]
    public async Task SetColorLoopAsync_OneColor_Throws()
    {
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentException>(() => api.SetColorLoopAsync(null, [new RgbColor(1, 2, 3)], spread: true, Ct));
    }

    [Fact]
    public void SettingsSaved_FollowsTheSaveEvents()
    {
        var saveEvents = new SettingsSaveEvents();
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices, saveEvents);
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
        var api = new EngineApi(_lighting, _profiles, _simulatedDevices, saveEvents);
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
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

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
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await api.SetLedColorsAsync("simulated-fan", 1, [new RgbColor(1, 2, 3)], Ct);

        var colors = await api.GetLedColorsAsync("simulated-fan", Ct);
        Assert.Equal(12, colors.Count);
        Assert.Equal(new RgbColor(1, 2, 3), colors[1]);
    }

    [Fact]
    public async Task SetLedColorsAsync_NoColors_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => api.SetLedColorsAsync("simulated-fan", 0, null!, Ct));
    }

    [Fact]
    public async Task ResizeZoneAsync_ResizesTheZoneAndReportsTheNewDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
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
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentException>(
            () => api.ResizeZoneAsync("simulated-fan", 0, 16, Ct));
    }

    [Fact]
    public async Task FrameRendered_ForwardsLightingFrames()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
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
        using var api = new EngineApi(lighting, new ProfileLibrary(new InMemoryProfileStore(), lighting), _simulatedDevices);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DevicesChanged += (_, _) => changed.TrySetResult();

        dynamic.SetDevices(DynamicDeviceProvider.Device("real"));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var device = Assert.Single(await api.GetDevicesAsync(Ct));
        Assert.Equal("real", device.Id);
    }

    [Fact]
    public async Task SetSimulatedDevicesAsync_Never_HidesThemAndReportsTheNewList()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DevicesChanged += (_, _) => changed.TrySetResult();

        await api.SetSimulatedDevicesAsync(SimulatedDevicesMode.Never, Ct);
        await changed.Task.WaitAsync(TestTimeouts.Short, Ct);

        Assert.Equal(SimulatedDevicesMode.Never, await api.GetSimulatedDevicesAsync(Ct));
        Assert.Empty(await api.GetDevicesAsync(Ct));
    }

    [Fact]
    public async Task SetSimulatedDevicesAsync_UnknownMode_Throws()
    {
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.SetSimulatedDevicesAsync((SimulatedDevicesMode)7, Ct));
        Assert.Equal(SimulatedDevicesMode.Automatic, await api.GetSimulatedDevicesAsync(Ct));
    }

    [Fact]
    public async Task Dispose_StopsForwardingFrames()
    {
        await _lighting.DiscoverAsync(Ct);
        var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
        var frames = 0;
        api.FrameRendered += (_, _) => frames++;

        api.Dispose();
        _lighting.Apply(LightingStates.Uniform(DeviceLighting.Default));

        Assert.Equal(0, frames);
    }

    [Fact]
    public async Task SaveProfileAsync_ThenLoadProfileAsync_BringsTheLightingBack()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
        await api.SetColorAsync(null, Orange, Ct);
        await api.SetSpeedAsync("simulated-fan", 2.0, Ct);

        await api.SaveProfileAsync("Gamen", Ct);
        await api.SetColorAsync(null, Blue, Ct);
        await api.SetSpeedAsync("simulated-fan", 0.5, Ct);
        await api.LoadProfileAsync("Gamen", Ct);

        Assert.Equal(["Gamen"], await api.GetProfilesAsync(Ct));
        Assert.Equal(Orange, (await api.GetLightingAsync(null, Ct)).Color);
        Assert.Equal(2.0, (await api.GetLightingAsync("simulated-fan", Ct)).Speed);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesTheName()
    {
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);
        await api.SaveProfileAsync("Gamen", Ct);

        await api.DeleteProfileAsync("Gamen", Ct);

        Assert.Empty(await api.GetProfilesAsync(Ct));
    }

    [Fact]
    public async Task LoadProfileAsync_UnknownName_Throws()
    {
        using var api = new EngineApi(_lighting, _profiles, _simulatedDevices);

        await Assert.ThrowsAsync<ArgumentException>(() => api.LoadProfileAsync("Gamen", Ct));
    }
}
