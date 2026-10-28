using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.AutoStart;
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
    public async Task SetDirectionAsync_SetsTheDirectionOfTheDeviceAndClearsIt()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        var changes = 0;
        api.LightingChanged += (_, _) => changes++;

        await api.SetDirectionAsync("simulated-keyboard", 45, Ct);

        Assert.Equal(45, (await api.GetLightingAsync("simulated-keyboard", Ct)).Direction);
        Assert.Null((await api.GetLightingAsync(null, Ct)).Direction);

        await api.SetDirectionAsync("simulated-keyboard", null, Ct);

        Assert.Null((await api.GetLightingAsync("simulated-keyboard", Ct)).Direction);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task SetDirectionAsync_AllDevices_SetsEveryDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetSpeedAsync("simulated-fan", 2.0, Ct);

        await api.SetDirectionAsync(null, 270, Ct);

        Assert.All(await api.GetAllLightingAsync(Ct), device => Assert.Equal(270, device.Lighting.Direction));
    }

    [Theory]
    [InlineData(EffectDirection.Min - 1)]
    [InlineData(EffectDirection.Max + 1)]
    public async Task SetDirectionAsync_OutOfRange_Throws(int degrees)
    {
        using var api = CreateApi();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.SetDirectionAsync(null, degrees, Ct));

        Assert.Equal("degrees", exception.ParamName);
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
    public async Task SetColorLoopAsync_TwelveColors_AreStoredAndThirteenThrow()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();

        await api.SetColorLoopAsync(null, Twelve, spread: true, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => api.SetColorLoopAsync(null, [.. Twelve, Red], spread: true, Ct));

        Assert.Equal(Twelve, (await api.GetLightingAsync(null, Ct)).Loop!.Colors);
    }

    // The list of the effect, a copy of what was sent; the other lists stay and the effect too.
    [Fact]
    public async Task SetEffectColorsAsync_StoresACopyOfTheColorsOfThatEffect()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Fire, [Blue], Ct);
        RgbColor[] colors = [Red, Green];

        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Comet, colors, Ct);
        colors[0] = Orange;

        var lighting = await api.GetLightingAsync("simulated-fan", Ct);
        Assert.Equal([Red, Green], lighting.EffectColors!.For(EffectIds.Comet, Orange));
        Assert.Equal([Blue], lighting.EffectColors.For(EffectIds.Fire, Orange));
        Assert.Equal(DeviceLighting.Default.EffectId, lighting.EffectId);
        Assert.Equal(EffectColors.None, (await api.GetLightingAsync("simulated-strip", Ct)).EffectColors);
    }

    // For all devices it sets the list of that effect on each, and keeps their other lists.
    [Fact]
    public async Task SetEffectColorsAsync_AllDevices_SetsTheListOnEveryDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Fire, [Blue], Ct);

        await api.SetEffectColorsAsync(null, EffectIds.Scanner, [Green], Ct);

        var fan = (await api.GetLightingAsync("simulated-fan", Ct)).EffectColors!;
        Assert.Equal([Green], fan.For(EffectIds.Scanner, Orange));
        Assert.Equal([Blue], fan.For(EffectIds.Fire, Orange));
        Assert.Equal([Green], (await api.GetLightingAsync("simulated-strip", Ct)).EffectColors!.For(EffectIds.Scanner, Orange));
    }

    // Without its list the comet has the lamp color again and the fire its warm colors; the other
    // lists and the effect stay.
    [Fact]
    public async Task ClearEffectColorsAsync_GivesThatEffectItsDefaultAgain()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetColorAsync("simulated-fan", Orange, Ct);
        await api.SetEffectAsync("simulated-fan", EffectIds.Comet, Ct);
        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Comet, [Red, Green], Ct);
        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Fire, [Blue], Ct);

        await api.ClearEffectColorsAsync("simulated-fan", EffectIds.Comet, Ct);

        var lighting = await api.GetLightingAsync("simulated-fan", Ct);
        Assert.Equal([Orange], lighting.EffectColors!.For(EffectIds.Comet, lighting.Color));
        Assert.Equal([Blue], lighting.EffectColors.For(EffectIds.Fire, lighting.Color));
        Assert.Equal(EffectIds.Comet, lighting.EffectId);

        await api.ClearEffectColorsAsync("simulated-fan", EffectIds.Fire, Ct);

        Assert.Equal(EffectColors.None, (await api.GetLightingAsync("simulated-fan", Ct)).EffectColors);
    }

    // For all devices it removes the list of that effect on each, also on a device that got one
    // of its own, and keeps their other lists.
    [Fact]
    public async Task ClearEffectColorsAsync_AllDevices_RemovesTheListOnEveryDevice()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        await api.SetEffectColorsAsync(null, EffectIds.Fire, [Red], Ct);
        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Fire, [Blue], Ct);
        await api.SetEffectColorsAsync("simulated-fan", EffectIds.Scanner, [Green], Ct);

        await api.ClearEffectColorsAsync(null, EffectIds.Fire, Ct);

        Assert.Equal(EffectColors.None, (await api.GetLightingAsync(null, Ct)).EffectColors);
        Assert.Equal(EffectColors.None, (await api.GetLightingAsync("simulated-strip", Ct)).EffectColors);
        Assert.Equal(EffectColors.None.With(EffectIds.Scanner, [Green]), (await api.GetLightingAsync("simulated-fan", Ct)).EffectColors);
    }

    [Theory]
    [InlineData(EffectIds.Static)]
    [InlineData(EffectIds.ColorLoop)]
    [InlineData("unknown")]
    public async Task ClearEffectColorsAsync_EffectWithoutColors_ThrowsForTheEffect(string effectId)
    {
        using var api = CreateApi();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => api.ClearEffectColorsAsync(null, effectId, Ct));

        Assert.Equal("effectId", exception.ParamName);
    }

    [Theory]
    [InlineData(EffectIds.Static)]
    [InlineData(EffectIds.ColorLoop)]
    [InlineData("unknown")]
    public async Task SetEffectColorsAsync_EffectWithoutColors_ThrowsForTheEffect(string effectId)
    {
        using var api = CreateApi();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => api.SetEffectColorsAsync(null, effectId, [Red], Ct));

        Assert.Equal("effectId", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(EffectColors.MaxColors + 1)]
    public async Task SetEffectColorsAsync_WrongNumberOfColors_ThrowsForTheColors(int count)
    {
        using var api = CreateApi();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => api.SetEffectColorsAsync(null, EffectIds.Comet, [.. Enumerable.Repeat(Red, count)], Ct));

        Assert.Equal("colors", exception.ParamName);
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

    // The app shows the choice with the devices, so it hears of it as of a new device list.
    [Fact]
    public async Task SetZonesAlikeAsync_ShowsInTheDevicesAndRaisesDevicesChanged()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();
        var changes = 0;
        api.DevicesChanged += (_, _) => changes++;

        await api.SetZonesAlikeAsync("simulated-headset", true, Ct);

        var devices = await api.GetDevicesAsync(Ct);
        Assert.Equal(["simulated-headset"], devices.Where(device => device.ZonesAlike).Select(device => device.Id));
        Assert.Equal(1, changes);
        Assert.Equal(["simulated-headset"], _lighting.ZonesAlike);
    }

    [Fact]
    public async Task SetZonesAlikeAsync_UnknownDevice_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var api = CreateApi();

        await Assert.ThrowsAsync<ArgumentException>(() => api.SetZonesAlikeAsync("unknown", true, Ct));
    }

    [Fact]
    public async Task Dispose_StopsForwardingZonesAlike()
    {
        await _lighting.DiscoverAsync(Ct);
        var api = CreateApi();
        var changes = 0;
        api.DevicesChanged += (_, _) => changes++;
        api.Dispose();

        _lighting.SetZonesAlike("simulated-headset", true);

        Assert.Equal(0, changes);
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
        using var autoPause = AutoPauses.WithFakes(_lighting.SetPaused);
        using var timer = new ProfileTimer(_profiles, new FakeClockEvents(), TimeProvider.System);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, autoPause: autoPause, profileTimer: timer, hotkeys: new AllOnOffHotkeys(), customColors: new CustomColorList());
        var changes = 0;
        api.OptionsChanged += (_, _) => changes++;

        await api.SetSimulatedDevicesAsync(SimulatedDevicesMode.Never, Ct);
        await api.SetFramesPerSecondAsync(60, Ct);
        await api.SetPauseOnLockAndSleepAsync(false, Ct);
        await api.SetCatchUpSceneTimesAsync(true, Ct);
        await api.SetHotkeysAsync(false, Ct);
        await api.SetCustomColorsAsync([new RgbColor(0, 0, 255)], Ct);
        await api.SetCustomColorsAsync([new RgbColor(0, 0, 255), new RgbColor(0, 0, 255)], Ct);
        await api.SetPausedLightsAsync(PausedLightsMode.Off, Ct);
        await api.SetFramesPerSecondAsync(60, Ct);
        await api.SetPausedLightsAsync(PausedLightsMode.Off, Ct);

        Assert.Equal(7, changes);
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

    // A color twice is kept once.
    [Fact]
    public async Task CustomColors_AreKeptByTheEngine()
    {
        var customColors = new CustomColorList();
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, customColors: customColors);

        Assert.Empty(await api.GetCustomColorsAsync(Ct));
        await api.SetCustomColorsAsync([Red, new RgbColor(0, 0, 255), Red], Ct);

        Assert.Equal([Red, new RgbColor(0, 0, 255)], customColors.Colors);
        Assert.Equal([Red, new RgbColor(0, 0, 255)], await api.GetCustomColorsAsync(Ct));
    }

    [Fact]
    public async Task CustomColors_MoreThanTwelve_AreRefused()
    {
        var customColors = new CustomColorList { Colors = [Red] };
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, customColors: customColors);
        RgbColor[] thirteen = [.. Enumerable.Range(1, 13).Select(i => new RgbColor((byte)i, 0, 0))];

        await Assert.ThrowsAsync<ArgumentException>(() => api.SetCustomColorsAsync(thirteen, Ct));

        Assert.Equal([Red], customColors.Colors);
    }

    // A new effect gets its id, which the lighting then takes as its effect; CustomEffectsChanged
    // follows each change.
    [Fact]
    public async Task CustomEffects_SaveShowAndDelete()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(LightingStates.Uniform(LightingStates.StaticLighting(Orange)));
        using var library = new CustomEffectLibrary(new InMemoryCustomEffectStore(), _lighting);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, customEffects: library);
        var changes = 0;
        api.CustomEffectsChanged += (_, _) => changes++;

        var saved = await api.SaveCustomEffectAsync(new CustomEffect("", "Disco", [new CustomEffectStep(Red)]), Ct);
        await api.SetEffectAsync("simulated-fan", saved.Id, Ct);

        Assert.Equal([saved], await api.GetCustomEffectsAsync(Ct));
        Assert.Equal(saved.Id, (await api.GetLightingAsync("simulated-fan", Ct)).EffectId);

        await api.DeleteCustomEffectAsync(saved.Id, Ct);

        Assert.Empty(await api.GetCustomEffectsAsync(Ct));
        Assert.Equal(EffectIds.Static, (await api.GetLightingAsync("simulated-fan", Ct)).EffectId);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task SetEffectAsync_OwnEffectThatDoesNotExist_Throws()
    {
        await _lighting.DiscoverAsync(Ct);
        using var library = new CustomEffectLibrary(new InMemoryCustomEffectStore(), _lighting);
        library.Load();
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, customEffects: library);

        await Assert.ThrowsAsync<ArgumentException>(() => api.SetEffectAsync(null, "custom:gone", Ct));
    }

    [Fact]
    public async Task CustomEffects_WithoutTheLibrary_AreNoneAndCannotBeSaved()
    {
        using var api = CreateApi();

        Assert.Empty(await api.GetCustomEffectsAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.SaveCustomEffectAsync(new CustomEffect("", "Disco", [new CustomEffectStep(Red)]), Ct));
    }

    [Fact]
    public async Task CustomColors_WithoutTheList_AreNone()
    {
        using var api = CreateApi();

        await api.SetCustomColorsAsync([Red], Ct);

        Assert.Empty(await api.GetCustomColorsAsync(Ct));
    }

    [Fact]
    public async Task AutoStart_IsTheSwitchOfTheEngine()
    {
        var registry = new FakeAutoStart();
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, autoStart: new AutoStartSwitch(registry));

        Assert.False(await api.GetAutoStartAsync(Ct));
        Assert.True(await api.SetAutoStartAsync(true, Ct));

        Assert.True(registry.IsEnabled);
        Assert.True(await api.GetAutoStartAsync(Ct));
    }

    // As in the tray menu: the registry may not be writable. The app hears it from the answer.
    [Fact]
    public async Task AutoStart_WhenWindowsRefuses_AnswersFalseAndStays()
    {
        var registry = new FakeAutoStart { SetEnabledException = new UnauthorizedAccessException() };
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, autoStart: new AutoStartSwitch(registry));
        var changes = 0;
        api.OptionsChanged += (_, _) => changes++;

        Assert.False(await api.SetAutoStartAsync(true, Ct));

        Assert.False(await api.GetAutoStartAsync(Ct));
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task AutoStart_WithoutTheSwitch_IsOffAndCannotChange()
    {
        using var api = CreateApi();

        Assert.False(await api.SetAutoStartAsync(true, Ct));
        Assert.False(await api.GetAutoStartAsync(Ct));
    }

    // Also when the tray menu changes it, which uses the same switch; once for each new value.
    [Fact]
    public async Task AutoStart_AnotherValue_RaisesOptionsChanged()
    {
        var autoStart = new AutoStartSwitch(new FakeAutoStart());
        var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, _openRgb, autoStart: autoStart);
        var changes = 0;
        api.OptionsChanged += (_, _) => changes++;

        await api.SetAutoStartAsync(true, Ct);
        await api.SetAutoStartAsync(true, Ct);
        autoStart.SetEnabled(false);
        api.Dispose();
        autoStart.SetEnabled(true);

        Assert.Equal(2, changes);
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
