using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineApiTests : IDisposable
{
    private readonly SwitchableDeviceProvider _simulated = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
    private readonly LightingController _lighting;
    private readonly InMemoryProfileStore _profileStore = new();
    private readonly ProfileLibrary _profiles;
    private readonly SimulatedDevicesSwitch _simulatedDevices;
    private readonly ZoneLengthKeeper _zoneLengths;
    private readonly FakeServerStatus _openRgb = new();

    public EngineApiTests()
    {
        _lighting = new LightingController([_simulated]);
        _zoneLengths = new ZoneLengthKeeper(_lighting);
        _profiles = new ProfileLibrary(_profileStore, _lighting);
        _simulatedDevices = new SimulatedDevicesSwitch(_simulated, _lighting);
    }

    public void Dispose()
    {
        _zoneLengths.Dispose();
        _simulatedDevices.Dispose();
        _lighting.Dispose();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task SetBrightnessAsync_OutOfRange_Throws(double brightness)
    {
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => api.SetBrightnessAsync(null, brightness, Ct));
    }

    [Fact]
    public async Task SetOnAsync_SwitchesTheDeviceOffAndOn()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();

        await api.SetOnAsync("simulated-fan", on: false, Ct);

        Assert.False((await api.GetLightingAsync("simulated-fan", Ct)).On);
        Assert.True((await api.GetLightingAsync(null, Ct)).On);

        await api.SetOnAsync(null, on: false, Ct);
        await api.SetOnAsync(null, on: true, Ct);

        Assert.True((await api.GetLightingAsync("simulated-fan", Ct)).On);
    }

    [Fact]
    public async Task GetAllLightingAsync_GivesEveryDeviceWithItsOwnOrTheGlobalLighting()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetColorAsync(null, Orange, Ct);
        await api.SetOnAsync("simulated-fan", on: false, Ct);

        var lighting = await api.GetAllLightingAsync(Ct);

        Assert.Equal((await api.GetDevicesAsync(Ct)).Select(device => device.Id), lighting.Select(device => device.DeviceId));
        Assert.False(Assert.Single(lighting, device => device.DeviceId == "simulated-fan").Lighting.On);
        Assert.All(lighting.Where(device => device.DeviceId != "simulated-fan"), device => Assert.True(device.Lighting.On));
        Assert.All(lighting, device => Assert.Equal(Orange, device.Lighting.Color));
    }

    [Fact]
    public async Task LightingChanged_FollowsEveryChangeOfTheLighting()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        var changes = 0;
        api.LightingChanged += (_, _) => changes++;

        await api.SetOnAsync(null, on: false, Ct);
        await api.SaveProfileAsync("Uit", Ct);
        _profiles.Load("Uit");

        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Dispose_StopsFollowingTheLighting()
    {
        await _lighting.DiscoverAsync(Ct);
        var api = CreateApi();
        var changes = 0;
        api.LightingChanged += (_, _) => changes++;

        api.Dispose();
        _lighting.SetLighting(null, lighting => lighting with { On = false });

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task SetSpeedAsync_SetsTheSpeedOfTheDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();

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
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.SetSpeedAsync(null, speed, Ct));
    }

    [Fact]
    public async Task SetColorLoopAsync_StoresACopyOfTheColors()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
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
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentException>(() => api.SetColorLoopAsync(null, [new RgbColor(1, 2, 3)], spread: true, Ct));
    }

    [Fact]
    public void SettingsSaved_FollowsTheSaveEvents()
    {
        var saveEvents = new SettingsSaveEvents();
        using var api = CreateApi(saveEvents);
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
        var api = CreateApi(saveEvents);
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
        using var api = CreateApi();

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
        using var api = CreateApi();

        await api.SetLedColorsAsync("simulated-fan", 1, [new RgbColor(1, 2, 3)], Ct);

        var colors = await api.GetLedColorsAsync("simulated-fan", Ct);
        Assert.Equal(12, colors.Count);
        Assert.Equal(new RgbColor(1, 2, 3), colors[1]);
    }

    [Fact]
    public async Task SetLedColorsAsync_NoColors_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => api.SetLedColorsAsync("simulated-fan", 0, null!, Ct));
    }

    [Fact]
    public async Task ResizeZoneAsync_ResizesTheZoneRemembersItAndReportsTheNewDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DevicesChanged += (_, _) => changed.TrySetResult();

        await api.ResizeZoneAsync("simulated-strip", 0, 45, Ct);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var devices = await api.GetDevicesAsync(Ct);
        var strip = devices.Single(device => device.Id == "simulated-strip");
        Assert.Equal(45, strip.LedCount);
        Assert.Equal(new RgbZone("Strip", 45, 1, 60), Assert.Single(strip.Zones));
        Assert.Equal(45, _zoneLengths.Lengths["simulated-strip"]["Strip"]);
    }

    [Fact]
    public async Task ResizeZoneAsync_FixedZone_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentException>(
            () => api.ResizeZoneAsync("simulated-fan", 0, 16, Ct));
    }

    [Fact]
    public async Task FrameRendered_ForwardsLightingFrames()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        var received = new TaskCompletionSource<LightingFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.FrameRendered += (_, frame) => received.TrySetResult(frame);

        await api.SetColorAsync(null, new RgbColor(1, 2, 3), Ct);

        // Not on the thread that rendered, but on the thread pool, a moment later.
        var frame = await received.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.All(frame.Devices.SelectMany(device => device.ToColors()), led => Assert.Equal(new RgbColor(1, 2, 3), led));
    }

    [Fact]
    public async Task SetFrameDevicesAsync_SendsOnlyThoseDevices_AndNullAllAgain()
    {
        var first = new RecordingDeviceProvider("first");
        var second = new RecordingDeviceProvider("second");
        using var lighting = new LightingController([first, second]);
        await lighting.DiscoverAsync(Ct);
        var time = new FakeTimeProvider();
        using var api = new EngineApi(lighting, new ZoneLengthKeeper(lighting), new ProfileLibrary(new InMemoryProfileStore(), lighting), _simulatedDevices, _openRgb, time: time);
        var frames = new List<LightingFrame>();
        api.FrameRendered += (_, frame) => frames.Add(frame);

        await api.SetFrameDevicesAsync(["second"], Ct);
        await api.SetColorAsync(null, new RgbColor(1, 2, 3), Ct);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(["second"], frames[^1].Devices.Select(device => device.DeviceId));
        Assert.Equal(2, (await api.GetFrameAsync(Ct)).Devices.Count);

        await api.SetFrameDevicesAsync(null, Ct);
        await api.SetColorAsync(null, new RgbColor(3, 2, 1), Ct);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(["first", "second"], frames[^1].Devices.Select(device => device.DeviceId));
    }

    [Fact]
    public async Task SetFramesWantedAsync_False_SendsNoFrames_AndTrueSendsTheLatestRightAway()
    {
        await _lighting.DiscoverAsync(Ct);
        var time = new FakeTimeProvider();
        using var api = new EngineApi(_lighting, _zoneLengths, new ProfileLibrary(new InMemoryProfileStore(), _lighting), _simulatedDevices, _openRgb, time: time);
        var frames = new List<LightingFrame>();
        api.FrameRendered += (_, frame) => frames.Add(frame);

        await api.SetFramesWantedAsync(false, Ct);
        await api.SetColorAsync(null, new RgbColor(1, 2, 3), Ct);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(frames);

        await api.SetFramesWantedAsync(true, Ct);
        time.Advance(TimeSpan.Zero);

        var frame = Assert.Single(frames);
        Assert.All(frame.Devices.SelectMany(device => device.ToColors()), led => Assert.Equal(new RgbColor(1, 2, 3), led));
    }

    [Fact]
    public async Task DevicesChanged_IsForwardedWithTheNewDeviceList()
    {
        var dynamic = new DynamicDeviceProvider();
        using var lighting = new LightingController([dynamic]);
        using var zoneLengths = new ZoneLengthKeeper(lighting);
        using var api = new EngineApi(lighting, zoneLengths, new ProfileLibrary(new InMemoryProfileStore(), lighting), _simulatedDevices, _openRgb);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DevicesChanged += (_, _) => changed.TrySetResult();

        dynamic.SetDevices(DynamicDeviceProvider.Device("real"));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var device = Assert.Single(await api.GetDevicesAsync(Ct));
        Assert.Equal("real", device.Id);
    }

    [Fact]
    public async Task OpenRgbStatus_IsTheStatusOfTheProviderAndFollowsIt()
    {
        using var api = CreateApi();
        var changes = new List<ServerStatus>();
        api.OpenRgbStatusChanged += (_, status) => changes.Add(status);
        Assert.Equal(ServerStatus.Connecting, await api.GetOpenRgbStatusAsync(Ct));

        _openRgb.Change(new ServerStatus(ServerState.Connected, 3));

        Assert.Equal(new ServerStatus(ServerState.Connected, 3), await api.GetOpenRgbStatusAsync(Ct));
        Assert.Equal([new ServerStatus(ServerState.Connected, 3)], changes);
    }

    [Fact]
    public void Dispose_StopsFollowingTheOpenRgbStatus()
    {
        var api = CreateApi();

        api.Dispose();

        Assert.False(_openRgb.HasListeners);
    }

    [Fact]
    public async Task Pause_IsThePauseOfTheLightingAndFollowsIt()
    {
        using var api = CreateApi();
        var changes = new List<PauseReasons>();
        api.PauseChanged += (_, pause) => changes.Add(pause);
        Assert.Equal(PauseReasons.None, await api.GetPauseAsync(Ct));

        _lighting.SetPaused(PauseReasons.Locked, true);

        Assert.Equal(PauseReasons.Locked, await api.GetPauseAsync(Ct));
        Assert.Equal([PauseReasons.Locked], changes);
    }

    [Fact]
    public async Task ResumeEffectsAsync_EndsTheManualPauseOnly()
    {
        using var api = CreateApi();
        _lighting.SetPaused(PauseReasons.Manual, true);
        _lighting.SetPaused(PauseReasons.Asleep, true);

        await api.ResumeEffectsAsync(Ct);

        Assert.Equal(PauseReasons.Asleep, _lighting.Pause);
    }

    [Fact]
    public void Dispose_StopsFollowingThePause()
    {
        var api = CreateApi();
        var changes = 0;
        api.PauseChanged += (_, _) => changes++;

        api.Dispose();
        _lighting.SetPaused(PauseReasons.Manual, true);

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task SetSimulatedDevicesAsync_Never_HidesThemAndReportsTheNewList()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
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
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.SetSimulatedDevicesAsync((SimulatedDevicesMode)7, Ct));
        Assert.Equal(SimulatedDevicesMode.Automatic, await api.GetSimulatedDevicesAsync(Ct));
    }

    [Fact]
    public async Task Dispose_StopsForwardingFrames()
    {
        await _lighting.DiscoverAsync(Ct);
        var api = CreateApi();
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
        using var api = CreateApi();
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
    public async Task GetProfileLightingAsync_GivesTheLightingOfAllDevicesInTheProfile()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetColorAsync(null, Orange, Ct);
        await api.SetOnAsync(null, on: false, Ct);
        await api.SaveProfileAsync("Uit", Ct);
        await api.SetColorAsync(null, Blue, Ct);

        var lighting = await api.GetProfileLightingAsync("uit", Ct);

        Assert.Equal(Orange, lighting.Color);
        Assert.False(lighting.On);
        await Assert.ThrowsAsync<ArgumentException>(() => api.GetProfileLightingAsync("Gamen", Ct));
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesTheName()
    {
        using var api = CreateApi();
        await api.SaveProfileAsync("Gamen", Ct);

        await api.DeleteProfileAsync("Gamen", Ct);

        Assert.Empty(await api.GetProfilesAsync(Ct));
    }

    [Fact]
    public async Task LoadProfileAsync_UnknownName_Throws()
    {
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentException>(() => api.LoadProfileAsync("Gamen", Ct));
    }

    [Fact]
    public async Task SetProfileAutomationAsync_ThenGet_ReturnsItAsSaved()
    {
        using var api = CreateApi();
        await api.SaveProfileAsync("Nacht", Ct);

        await api.SetProfileAutomationAsync("Nacht", new ProfileAutomation(new TimeOnly(23, 0, 15), " eldenring "), Ct);

        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"), await api.GetProfileAutomationAsync("nacht", Ct));
        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"), _profileStore.Profiles[0].Automation);
    }

    [Fact]
    public async Task SetProfileAutomationAsync_InvalidProgram_Throws()
    {
        using var api = CreateApi();
        await api.SaveProfileAsync("Gamen", Ct);

        await Assert.ThrowsAsync<ArgumentException>(() => api.SetProfileAutomationAsync("Gamen", new ProfileAutomation(Program: "game?.exe"), Ct));
    }

    [Fact]
    public async Task GetProfileAutomationAsync_UnknownName_Throws()
    {
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentException>(() => api.GetProfileAutomationAsync("Gamen", Ct));
    }

    // So a second settings app sees what the first one changed.
    [Fact]
    public async Task ProfilesChanged_FollowsSaveAndDelete()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        var changes = 0;
        api.ProfilesChanged += (_, _) => changes++;

        await api.SaveProfileAsync("Nacht", Ct);
        await api.SetProfileAutomationAsync("Nacht", new ProfileAutomation(new TimeOnly(23, 0)), Ct);
        await api.DeleteProfileAsync("Nacht", Ct);

        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task OptionsChanged_FollowsEveryOption()
    {
        using var autoPause = new AutoPause(new FakePowerEvents(), new FakeSessionEvents(), new FakeDisplayEvents(), _lighting.SetPaused);
        using var timer = new ProfileTimer(_profiles, new FakeClockEvents(), TimeProvider.System);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, autoPause: autoPause, profileTimer: timer, hotkeys: new AllOnOffHotkeys());
        var changes = 0;
        api.OptionsChanged += (_, _) => changes++;

        await api.SetSimulatedDevicesAsync(SimulatedDevicesMode.Never, Ct);
        await api.SetFramesPerSecondAsync(60, Ct);
        await api.SetPauseOnLockAndSleepAsync(false, Ct);
        await api.SetCatchUpSceneTimesAsync(true, Ct);
        await api.SetHotkeysAsync(false, Ct);
        await api.SetPausedLightsAsync(PausedLightsMode.Off, Ct);
        await api.SetFramesPerSecondAsync(60, Ct);
        await api.SetPausedLightsAsync(PausedLightsMode.Off, Ct);

        Assert.Equal(6, changes);
    }

    [Fact]
    public async Task PausedLights_IsTheOptionOfTheLighting()
    {
        using var api = CreateApi();

        Assert.Equal(PausedLightsMode.Keep, await api.GetPausedLightsAsync(Ct));
        await api.SetPausedLightsAsync(PausedLightsMode.Dim, Ct);

        Assert.Equal(PausedLightsMode.Dim, _lighting.PausedLights);
        Assert.Equal(PausedLightsMode.Dim, await api.GetPausedLightsAsync(Ct));
    }

    [Fact]
    public async Task PausedLights_AModeThisVersionDoesNotKnow_KeepsTheLastFrame()
    {
        using var api = CreateApi();
        await api.SetPausedLightsAsync(PausedLightsMode.Off, Ct);

        await api.SetPausedLightsAsync((PausedLightsMode)7, Ct);

        Assert.Equal(PausedLightsMode.Keep, _lighting.PausedLights);
    }

    [Fact]
    public async Task Hotkeys_AreTheOptionOfTheEngine()
    {
        var hotkeys = new AllOnOffHotkeys();
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, hotkeys: hotkeys);

        Assert.True(await api.GetHotkeysAsync(Ct));
        await api.SetHotkeysAsync(false, Ct);

        Assert.False(hotkeys.Enabled);
        Assert.False(await api.GetHotkeysAsync(Ct));
    }

    [Fact]
    public async Task Hotkeys_WithoutThem_AreOff()
    {
        using var api = CreateApi();

        await api.SetHotkeysAsync(true, Ct);

        Assert.False(await api.GetHotkeysAsync(Ct));
    }

    [Fact]
    public async Task CatchUpSceneTimes_IsTheOptionOfTheTimer()
    {
        using var timer = new ProfileTimer(_profiles, new FakeClockEvents(), TimeProvider.System);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, profileTimer: timer);

        Assert.False(await api.GetCatchUpSceneTimesAsync(Ct));
        await api.SetCatchUpSceneTimesAsync(true, Ct);

        Assert.True(timer.CatchUp);
        Assert.True(await api.GetCatchUpSceneTimesAsync(Ct));
    }

    [Fact]
    public async Task CatchUpSceneTimes_WithoutATimer_IsOff()
    {
        using var api = CreateApi();

        await api.SetCatchUpSceneTimesAsync(true, Ct);

        Assert.False(await api.GetCatchUpSceneTimesAsync(Ct));
    }

    [Fact]
    public void Dispose_StopsTheEventsOfProfilesAndOptions()
    {
        var api = CreateApi();
        var changes = 0;
        api.ProfilesChanged += (_, _) => changes++;
        api.OptionsChanged += (_, _) => changes++;

        api.Dispose();
        _profiles.Save("Nacht");
        _lighting.FramesPerSecond = 12;

        Assert.Equal(0, changes);
    }

    private EngineApi CreateApi(SettingsSaveEvents? saveEvents = null) =>
        new(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, saveEvents);
}
