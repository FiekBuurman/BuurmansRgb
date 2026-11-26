using BuurmansRgb.Core;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts.Tests;

public sealed class EngineRpcTests : IDisposable
{
    // 07:00 on workdays and 09:00 in the weekend.
    private static readonly ProfileAutomation MoreTimes = new([new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend)], ["eldenring.exe"]);

    // A device with zones that resize, one with a location and a matrix with LED names, each with
    // its own source.
    private static readonly IReadOnlyList<DeviceInfo> RpcDevices =
    [
        new("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]) { Source = DeviceSource.Simulated },
        new("board", "Motherboard", DeviceType.Motherboard, 5, [RgbZone.Fixed("Mainboard", 4), new RgbZone("ARGB header", 0, 0, 120), RgbZone.Fixed("RGB header", 1)])
        {
            Source = DeviceSource.Hardware,
            Location = new DeviceLocation("PIIX4", "0x4E"),
            HasDirectMode = false,
            ZonesAlike = true,
        },
        new("keyboard", "Keyboard", DeviceType.Keyboard, 5,
            [RgbZone.Fixed("Keys", 5) with { Matrix = new LedMatrix(2, 3, [0, 1, 2, 3, LedMatrix.NoLed, 4]), LedNames = ["Escape", "F1", "F2", "Tab", "Q"] }])
        {
            Source = DeviceSource.Hardware,
        },
    ];

    // Lighting with every field set to something other than its default.
    private static readonly LightingInfo FullLighting = new(
        new RgbColor(10, 20, 30), 0.25, EffectIds.Static, 1.5, new ColorLoop([new RgbColor(1, 2, 3), new RgbColor(4, 5, 6)], Spread: false), On: false,
        EffectColors: EffectColors.None.With(EffectIds.Comet, [new RgbColor(7, 8, 9)]).With(EffectIds.Fire, [new RgbColor(1, 1, 1), new RgbColor(9, 9, 9)]),
        Direction: 135);

    private readonly string _pipeName = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly FakeEngineApi _api = new();
    private JsonRpc? _server;
    private EngineConnection? _client;

    // The answers the RPC tests expect, most of them unlike the defaults of the fake engine, so that they show they crossed the pipe.
    public EngineRpcTests()
    {
        _api.Devices = RpcDevices;
        _api.Lighting.Clear();
        _api.Lighting[""] = FullLighting;
        _api.LedColors["fan"] = [new RgbColor(1, 1, 1), new RgbColor(2, 2, 2)];
        _api.Overlays["keyboard"] = [null, new RgbColor(3, 3, 3)];
        _api.Pause = PauseReasons.Manual | PauseReasons.Locked;
        _api.OpenRgbStatus = new ServerStatus(ServerState.Connected, 4);
        _api.ZoneCount = new ZoneCountInfo("board", 1, 37, 120, 2, true) { Sequence = 7 };
        _api.SimulatedDevices = SimulatedDevicesMode.Always;
        _api.HadRealDevices = true;
        _api.FramesPerSecond = 24;
        _api.PauseOnLockAndSleep = true;
        _api.PausedLights = PausedLightsMode.Dim;
        _api.SceneFade = TimeSpan.FromSeconds(1.5);
        _api.CatchUpSceneTimes = false;
        _api.Hotkeys = true;
        _api.AutoStart = true;
        _api.CustomColors = [new RgbColor(255, 0, 0)];
        _api.OtherRgbSoftware = [new OtherRgbSoftware("signalrgb", "SignalRGB"), new OtherRgbSoftware(OtherRgbSoftware.OpenRgbId, "OpenRGB")];
        _api.Profiles.AddRange(["Gamen", "Nacht"]);
        _api.ActiveProfile = "Nacht";
        _api.ProfileLighting["Nacht"] = FullLighting;
        _api.GoneProfiles.Add("Gamen");
        _api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe");
        _api.ProfileAutomations["Ochtend"] = MoreTimes;
        _api.ProfileDevices["Nacht"] = ["openrgb-strip-1a2b", "fan"];
        _api.WholeHouseTimes = new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0))], [new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays)], "Alles uit");
        _api.RestoreResult = new SettingsRestoreResult(SettingsRestoreOutcome.Unreadable, SettingsBackup.ProfilesFileName);
        _api.ImportResult = new ShareImportResult(ShareImportOutcome.Imported, SharedKind.Scene, "Gamen (2)", "Gamen", UnmatchedDevices: ["K70"], AddedEffects: ["Disco"], UnnamedUnmatchedDevices: 2);
        _api.CustomEffects = [new CustomEffect("custom:1", "Disco", [new CustomEffectStep([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)], 0.5, 2)])];
    }

    public void Dispose()
    {
        _client?.Dispose();
        _server?.Dispose();
    }

    [Fact]
    public void PipeName_IsPerWindowsSession()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();

        Assert.Equal($"BuurmansRgb.Engine.{process.SessionId}", EngineRpc.PipeName);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsDevicesWithTheirZonesFromServer()
    {
        var client = await ConnectAsync();

        var devices = await client.Api.GetDevicesAsync(Ct);

        Assert.Equal(_api.Devices, devices);
        Assert.Equal(DeviceSource.Simulated, devices[0].Source);
        Assert.Equal(new DeviceLocation("PIIX4", "0x4E"), devices[1].Location);
        Assert.Null(devices[0].Location);
        Assert.NotNull(devices[^1].Zones[0].Matrix);
        Assert.Equal(["Escape", "F1", "F2", "Tab", "Q"], devices[^1].Zones[0].LedNames!);
        Assert.Null(devices[0].Zones[0].LedNames);
        Assert.Equal([true, false, true], devices.Select(device => device.HasDirectMode));
        Assert.Equal([false, true, false], devices.Select(device => device.ZonesAlike));
    }

    [Fact]
    public async Task SetColorAsync_SendsDeviceAndColor()
    {
        var client = await ConnectAsync();

        await client.Api.SetColorAsync("fan", new RgbColor(255, 136, 0), Ct);
        await client.Api.SetColorAsync(null, RgbColor.White, Ct);

        Assert.Equal(["color fan #FF8800", "color all #FFFFFF"], _api.Calls);
    }

    [Fact]
    public async Task GetLightingAsync_RoundTripsEveryField()
    {
        var client = await ConnectAsync();

        var lighting = await client.Api.GetLightingAsync("fan", Ct);

        Assert.Equal(FullLighting, lighting);
    }

    [Fact]
    public async Task GetAllLightingAsync_GivesEveryDeviceWithEveryField()
    {
        var client = await ConnectAsync();

        var lighting = await client.Api.GetAllLightingAsync(Ct);

        Assert.Equal(_api.Devices.Select(device => device.Id), lighting.Select(device => device.DeviceId));
        Assert.All(lighting, device => Assert.Equal(FullLighting, device.Lighting));
    }

    [Fact]
    public async Task SetFramesWantedAsync_CrossesThePipe()
    {
        var client = await ConnectAsync();

        await client.Api.SetFramesWantedAsync(false, Ct);
        await client.Api.SetFramesWantedAsync(true, Ct);

        Assert.Equal([false, true], _api.FramesWantedCalls);
    }

    [Fact]
    public async Task SetFrameDevicesAsync_CrossesThePipe()
    {
        var client = await ConnectAsync();

        await client.Api.SetFrameDevicesAsync(["fan"], Ct);
        await client.Api.SetFrameDevicesAsync(null, Ct);

        Assert.Equal(2, _api.FrameDevicesCalls.Count);
        Assert.Equal(["fan"], _api.FrameDevicesCalls[0]!);
        Assert.Null(_api.FrameDevicesCalls[1]);
    }

    [Fact]
    public async Task Profiles_NamesAndCallsCrossThePipe()
    {
        var client = await ConnectAsync();

        var names = await client.Api.GetProfilesAsync(Ct);
        await client.Api.SaveProfileAsync("Werk", Ct);
        await client.Api.LoadProfileAsync("Gamen", Ct);
        await client.Api.DeleteProfileAsync("Nacht", Ct);

        Assert.Equal(["Gamen", "Nacht"], names);
        Assert.Equal(["profile save Werk", "profile load Gamen", "profile delete Nacht"], _api.Calls);
    }

    [Fact]
    public async Task ActiveProfile_CrossesThePipe_AlsoWhenThereIsNone()
    {
        var client = await ConnectAsync();

        Assert.Equal("Nacht", await client.Api.GetActiveProfileAsync(Ct));
        _api.ActiveProfile = null;
        Assert.Null(await client.Api.GetActiveProfileAsync(Ct));
    }

    [Fact]
    public async Task GetProfileLightingAsync_RoundTripsEveryField()
    {
        var client = await ConnectAsync();

        Assert.Equal(FullLighting, await client.Api.GetProfileLightingAsync("Nacht", Ct));
        await Assert.ThrowsAsync<RemoteInvocationException>(() => client.Api.GetProfileLightingAsync("Gamen", Ct));
    }

    [Fact]
    public async Task ProfileAutomation_CrossesThePipe()
    {
        var client = await ConnectAsync();

        var automation = await client.Api.GetProfileAutomationAsync("Nacht", Ct);
        var moreTimes = await client.Api.GetProfileAutomationAsync("Ochtend", Ct);
        await client.Api.SetProfileAutomationAsync("Nacht", new ProfileAutomation(new TimeOnly(7, 30), Days: WeekDays.Workdays), Ct);
        await client.Api.SetProfileAutomationAsync("Gamen", new ProfileAutomation(Program: "eldenring.exe"), Ct);
        await client.Api.SetProfileAutomationAsync("Race", new ProfileAutomation(Program: "forza.exe", ForegroundOnly: true), Ct);
        await client.Api.SetProfileAutomationAsync("Werk", ProfileAutomation.None, Ct);
        await client.Api.SetProfileAutomationAsync("Ochtend", MoreTimes, Ct);

        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"), automation);
        Assert.Equal(MoreTimes, moreTimes);
        Assert.Equal(
            [
                ("Nacht", new ProfileAutomation(new TimeOnly(7, 30), Days: WeekDays.Workdays)),
                ("Gamen", new ProfileAutomation(Program: "eldenring.exe")),
                ("Race", new ProfileAutomation(Program: "forza.exe", ForegroundOnly: true)),
                ("Werk", ProfileAutomation.None),
                ("Ochtend", MoreTimes),
            ],
            _api.ProfileAutomationCalls);
    }

    // Null, every device, stays null over the pipe instead of an empty list.
    [Fact]
    public async Task ProfileDevices_CrossThePipe()
    {
        var client = await ConnectAsync();

        var some = await client.Api.GetProfileDevicesAsync("Nacht", Ct);
        var every = await client.Api.GetProfileDevicesAsync("Werk", Ct);
        await client.Api.SetProfileDevicesAsync("Nacht", ["fan"], Ct);
        await client.Api.SetProfileDevicesAsync("Werk", null, Ct);

        Assert.Equal(["openrgb-strip-1a2b", "fan"], some);
        Assert.Null(every);
        Assert.Equal(["profile devices Nacht fan", "profile devices Werk all"], _api.Calls);
        Assert.Equal(["fan"], _api.ProfileDevices["Nacht"]);
        Assert.False(_api.ProfileDevices.ContainsKey("Werk"));
    }

    [Fact]
    public async Task WholeHouseTimes_CrossThePipe()
    {
        var client = await ConnectAsync();
        var off = new WholeHouseTimes([new LoadTime(new TimeOnly(22, 30), WeekDays.Weekend)]);

        var times = await client.Api.GetWholeHouseTimesAsync(Ct);
        await client.Api.SetWholeHouseTimesAsync(off, Ct);

        Assert.Equal(new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0))], [new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays)], "Alles uit"), times);
        Assert.Equal(["whole house off 22:30/Weekend on -"], _api.Calls);
        Assert.Equal([off], _api.WholeHouseTimesCalls);
        Assert.Empty(Assert.Single(_api.WholeHouseTimesCalls).On);
    }

    [Fact]
    public async Task FrameRendered_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<LightingFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.FrameRendered += (_, frame) => received.TrySetResult(frame);

        _api.RaiseFrame(new LightingFrame(7, [DeviceFrame.FromColors("fan", [new RgbColor(1, 2, 3)])]));

        var frame = await received.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal(7, frame.Sequence);
        Assert.Equal("fan", Assert.Single(frame.Devices).DeviceId);
        Assert.Equal([new RgbColor(1, 2, 3)], frame.Devices[0].ToColors());
    }

    [Fact]
    public async Task DevicesChanged_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.DevicesChanged += (_, _) => received.TrySetResult();

        _api.RaiseDevicesChanged();

        await received.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task SettingsSaved_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<SettingsSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.SettingsSaved += (_, result) => received.TrySetResult(result);

        _api.RaiseSaved(succeeded: false);

        Assert.False((await received.Task.WaitAsync(TestTimeouts.Short, Ct)).Succeeded);
    }

    [Fact]
    public async Task OpenRgbStatus_CrossesThePipeAsAnswerAndAsEvent()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<ServerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.OpenRgbStatusChanged += (_, status) => received.TrySetResult(status);

        var status = await client.Api.GetOpenRgbStatusAsync(Ct);
        _api.RaiseOpenRgbStatus(new ServerStatus(ServerState.Failed));

        Assert.Equal(new ServerStatus(ServerState.Connected, 4), status);
        Assert.Equal(new ServerStatus(ServerState.Failed), await received.Task.WaitAsync(TestTimeouts.Short, Ct));
    }

    [Fact]
    public async Task Pause_CrossesThePipeAsAnswerAndAsEvent()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<PauseReasons>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.PauseChanged += (_, pause) => received.TrySetResult(pause);

        var pause = await client.Api.GetPauseAsync(Ct);
        _api.RaisePause(PauseReasons.Asleep);

        Assert.Equal(PauseReasons.Manual | PauseReasons.Locked, pause);
        Assert.Equal(PauseReasons.Asleep, await received.Task.WaitAsync(TestTimeouts.Short, Ct));
    }

    [Fact]
    public async Task ResumeEffectsAsync_ReachesServer()
    {
        var client = await ConnectAsync();

        await client.Api.ResumeEffectsAsync(Ct);

        Assert.Equal(["resume"], _api.Calls);
    }

    [Fact]
    public async Task RestartAsync_ReachesServer()
    {
        var client = await ConnectAsync();

        await client.Api.RestartAsync(Ct);

        Assert.Equal(["restart"], _api.Calls);
    }

    [Fact]
    public async Task SetColorLoopAsync_ReachesServerWithColorsInOrder()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 255, 0), new(0, 255, 0), new(0, 0, 255)];

        await client.Api.SetColorLoopAsync("fan", colors, spread: false, Ct);

        Assert.Equal(["loop fan #FFFF00,#00FF00,#0000FF together"], _api.Calls);
    }

    [Fact]
    public async Task SetEffectColorsAsync_ReachesServerWithColorsInOrder()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(0, 0, 60), new(0, 0, 255), new(0, 200, 255)];

        await client.Api.SetEffectColorsAsync(null, EffectIds.Fire, colors, Ct);

        Assert.Equal([$"effect-colors all {EffectIds.Fire} #00003C,#0000FF,#00C8FF"], _api.Calls);
    }

    [Fact]
    public async Task ClearEffectColorsAsync_ReachesServerWithDeviceAndEffect()
    {
        var client = await ConnectAsync();

        await client.Api.ClearEffectColorsAsync("fan", EffectIds.Comet, Ct);
        await client.Api.ClearEffectColorsAsync(null, EffectIds.Fire, Ct);

        Assert.Equal([$"effect-colors fan {EffectIds.Comet} default", $"effect-colors all {EffectIds.Fire} default"], _api.Calls);
    }

    [Fact]
    public async Task SetOnAsync_ReachesServer()
    {
        var client = await ConnectAsync();

        await client.Api.SetOnAsync("fan", on: false, Ct);
        await client.Api.SetOnAsync(null, on: true, Ct);

        Assert.Equal(["on fan off", "on all on"], _api.Calls);
    }

    [Fact]
    public async Task LightingChanged_OnServer_ReachesClient()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.LightingChanged += (_, _) => received.TrySetResult();

        _api.RaiseLightingChanged();

        await received.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task ProfilesAndOptionsChanged_OnServer_ReachClient()
    {
        var client = await ConnectAsync();
        var profiles = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.ProfilesChanged += (_, _) => profiles.TrySetResult();
        client.Api.OptionsChanged += (_, _) => options.TrySetResult();

        _api.RaiseProfilesChanged();
        _api.RaiseOptionsChanged();

        await profiles.Task.WaitAsync(TestTimeouts.Short, Ct);
        await options.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task SetSpeedAsync_ReachesServer()
    {
        var client = await ConnectAsync();

        await client.Api.SetSpeedAsync(null, 2.5, Ct);

        Assert.Equal(["speed all 2.50"], _api.Calls);
    }

    [Fact]
    public async Task SetDirectionAsync_ReachesServerWithDegreesOrNull()
    {
        var client = await ConnectAsync();

        await client.Api.SetDirectionAsync("keyboard", 45, Ct);
        await client.Api.SetDirectionAsync(null, null, Ct);

        Assert.Equal(["direction keyboard 45", "direction all leds"], _api.Calls);
    }

    [Fact]
    public async Task LedColors_RoundTripThroughThePipe()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 0, 0), new(0, 255, 0)];

        await client.Api.SetLedColorsAsync("fan", 3, colors, Ct);
        var stored = await client.Api.GetLedColorsAsync("fan", Ct);

        Assert.Equal(["leds fan 3 #FF0000,#00FF00"], _api.Calls);
        Assert.Equal(_api.LedColors["fan"], stored);
    }

    // A null takes the layer away from that LED, so it goes through the pipe as null.
    [Fact]
    public async Task Overlay_RoundTripsThroughThePipe_WithItsNulls()
    {
        var client = await ConnectAsync();
        RgbColor?[] colors = [new RgbColor(255, 0, 0), null, new RgbColor(0, 0, 255)];

        await client.Api.SetOverlayAsync("keyboard", 2, colors, Ct);
        var stored = await client.Api.GetOverlayAsync("keyboard", Ct);

        Assert.Equal(["overlay keyboard 2 #FF0000,-,#0000FF"], _api.Calls);
        Assert.Equal(_api.Overlays["keyboard"], stored);
    }

    [Fact]
    public async Task ResizeZoneAsync_SendsDeviceZoneAndLength()
    {
        var client = await ConnectAsync();

        await client.Api.ResizeZoneAsync("board", 1, 24, Ct);

        Assert.Equal(["resize board 1 24"], _api.Calls);
    }

    [Fact]
    public async Task SetZonesAlikeAsync_SendsTheDeviceAndTheChoice()
    {
        var client = await ConnectAsync();

        await client.Api.SetZonesAlikeAsync("board", true, Ct);
        await client.Api.SetZonesAlikeAsync("strip", false, Ct);

        Assert.Equal(["zones-alike board on", "zones-alike strip off"], _api.Calls);
    }

    [Fact]
    public async Task FlashDeviceAsync_SendsTheDevice()
    {
        var client = await ConnectAsync();

        await client.Api.FlashDeviceAsync("fan", Ct);

        Assert.Equal(["flash fan"], _api.Calls);
    }

    [Fact]
    public async Task FlashZoneAsync_SendsTheDeviceAndZone()
    {
        var client = await ConnectAsync();

        await client.Api.FlashZoneAsync("board", 2, Ct);

        Assert.Equal(["flash board zone 2"], _api.Calls);
    }

    // A change with only some values keeps the others null on the way.
    [Fact]
    public async Task ZoneCountCalls_SendTheDeviceZoneAndChange()
    {
        var client = await ConnectAsync();

        await client.Api.StartZoneCountAsync("board", 2, 5, Ct);
        await client.Api.SetZoneCountAsync("board", 2, new ZoneCountChange(Led: 37), Ct);
        await client.Api.SetZoneCountAsync("board", 2, new ZoneCountChange(Running: false, LedsPerSecond: 2), Ct);
        await client.Api.StopZoneCountAsync("board", 2, Ct);

        Assert.Equal(
            [
                "count board 2 5",
                "count board 2 led 37",
                "count board 2 pause pace 2",
                "count board 2 stop",
            ],
            _api.Calls);
    }

    [Fact]
    public async Task ZoneCount_CrossesThePipeAsAnswerAndAsEvent()
    {
        var client = await ConnectAsync();
        var received = new TaskCompletionSource<ZoneCountInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.ZoneCountChanged += (_, count) => received.TrySetResult(count);

        var count = await client.Api.GetZoneCountAsync(Ct);
        _api.RaiseZoneCount(ZoneCountInfo.None with { Sequence = 8 });

        Assert.Equal(new ZoneCountInfo("board", 1, 37, 120, 2, true) { Sequence = 7 }, count);
        Assert.True(count.IsCounting);
        var none = await received.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal(ZoneCountInfo.None with { Sequence = 8 }, none);
        Assert.False(none.IsCounting);
    }

    [Fact]
    public async Task RescanDevicesAsync_GivesTheAnswerBack()
    {
        var client = await ConnectAsync();
        _api.RescanOutcome = RescanOutcome.Unreachable;

        Assert.False(await client.Api.RescanDevicesAsync(Ct));

        Assert.Equal(["rescan"], _api.Calls);
    }

    [Fact]
    public async Task RescanDevicesWithOutcomeAsync_GivesTheOutcomeBack()
    {
        var client = await ConnectAsync();
        _api.RescanOutcome = RescanOutcome.ListNotLoaded;

        Assert.Equal(RescanOutcome.ListNotLoaded, await client.Api.RescanDevicesWithOutcomeAsync(Ct));

        Assert.Equal(["rescan"], _api.Calls);
    }

    // An engine from before the method, which only knows RescanDevicesAsync: the settings app sees
    // that it does not know the method, not an error of the engine, and asks the old way.
    [Fact]
    public async Task RescanDevicesWithOutcomeAsync_OlderEngine_IsUnknownToEngine()
    {
        var pipe = EngineRpc.CreateServerStream(_pipeName);
        var accepted = pipe.WaitForConnectionAsync(Ct);
        _client = await EngineConnection.ConnectAsync(_pipeName, TestTimeouts.Short, Ct);
        await accepted;
        _server = new JsonRpc(new HeaderDelimitedMessageHandler(pipe, new SystemTextJsonFormatter()));
        _server.AddLocalRpcTarget(new OlderEngineApi());
        _server.StartListening();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => _client.Api.RescanDevicesWithOutcomeAsync(Ct));

        Assert.True(EngineRpc.IsUnknownToEngine(exception), exception.ToString());
        Assert.False(EngineRpc.IsRejectedByEngine(exception));
        Assert.True(await _client.Api.RescanDevicesAsync(Ct));
    }

    [Fact]
    public void IsUnknownToEngine_RejectedOrLost_IsFalse()
    {
        Assert.False(EngineRpc.IsUnknownToEngine(new RemoteInvocationException("Invalid", 0, errorData: null)));
        Assert.False(EngineRpc.IsUnknownToEngine(new ConnectionLostException()));
    }

    // The process id of the engine, for its memory and CPU on Instellingen.
    [Fact]
    public async Task GetProcessIdAsync_GivesTheIdOfTheEngine()
    {
        var client = await ConnectAsync();

        Assert.Equal(4242, await client.Api.GetProcessIdAsync(Ct));
    }

    // A backup is bytes both ways, also every byte value and a size of some MB, and the answer to a
    // restore comes back with its outcome and file.
    [Fact]
    public async Task SettingsBackup_CrossesThePipeBothWays()
    {
        var client = await ConnectAsync();
        var archive = new byte[3 * 1024 * 1024];
        new Random(42).NextBytes(archive);
        _api.ExportedSettings = archive;

        var exported = await client.Api.ExportSettingsAsync(Ct);
        var result = await client.Api.RestoreSettingsAsync(archive, Ct);

        Assert.Equal(archive, exported);
        Assert.Equal(archive, Assert.Single(_api.RestoredArchives));
        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Unreadable, SettingsBackup.ProfilesFileName), result);
    }

    // A shared scene or effect is bytes both ways, and the answer to an import comes back with its
    // names and the devices that were not there.
    [Fact]
    public async Task Sharing_CrossesThePipeBothWays()
    {
        var client = await ConnectAsync();
        _api.ExportedShare = "{\"version\":1,\"name\":\"Gamen\"}"u8.ToArray();

        var scene = await client.Api.ExportSceneAsync("Gamen", Ct);
        var effect = await client.Api.ExportCustomEffectAsync("custom:1a2b3c4d", Ct);
        var result = await client.Api.ImportSharedAsync(scene, Ct);

        Assert.Equal(_api.ExportedShare, scene);
        Assert.Equal(_api.ExportedShare, effect);
        Assert.Equal(["export scene Gamen", "export effect custom:1a2b3c4d", "import"], _api.Calls);
        Assert.Equal(scene, Assert.Single(_api.ImportedShares));
        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal(SharedKind.Scene, result.Kind);
        Assert.Equal("Gamen (2)", result.Name);
        Assert.True(result.IsRenamed);
        Assert.Equal(["K70"], result.UnmatchedDevices);
        Assert.Equal(["Disco"], result.AddedEffects);
        Assert.Equal(2, result.UnnamedUnmatchedDevices);
    }

    // The other RGB software comes over the pipe with its ids and names, and a dismissed id goes back.
    [Fact]
    public async Task OtherRgbSoftware_CrossesThePipeBothWays()
    {
        var client = await ConnectAsync();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.OtherRgbSoftwareChanged += (_, _) => changed.TrySetResult();

        var found = await client.Api.GetOtherRgbSoftwareAsync(Ct);
        await client.Api.DismissOtherRgbSoftwareAsync("signalrgb", Ct);
        _api.RaiseOtherRgbSoftwareChanged();

        Assert.Equal([new OtherRgbSoftware("signalrgb", "SignalRGB"), new OtherRgbSoftware("openrgb", "OpenRGB")], found);
        Assert.Equal(["dismiss signalrgb"], _api.Calls);
        await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task SimulatedDevices_ModeCrossesThePipeBothWays()
    {
        var client = await ConnectAsync();

        var mode = await client.Api.GetSimulatedDevicesAsync(Ct);
        await client.Api.SetSimulatedDevicesAsync(SimulatedDevicesMode.Never, Ct);
        var hadRealDevices = await client.Api.GetHadRealDevicesAsync(Ct);

        Assert.Equal(SimulatedDevicesMode.Always, mode);
        Assert.Equal(["simulated Never"], _api.Calls);
        Assert.True(hadRealDevices);
    }

    [Fact]
    public async Task Options_CrossThePipeBothWays()
    {
        var client = await ConnectAsync();

        var framesPerSecond = await client.Api.GetFramesPerSecondAsync(Ct);
        var pauseOnLock = await client.Api.GetPauseOnLockAndSleepAsync(Ct);
        var catchUp = await client.Api.GetCatchUpSceneTimesAsync(Ct);
        await client.Api.SetFramesPerSecondAsync(60, Ct);
        await client.Api.SetPauseOnLockAndSleepAsync(false, Ct);
        await client.Api.SetCatchUpSceneTimesAsync(true, Ct);
        var hotkeys = await client.Api.GetHotkeysAsync(Ct);
        await client.Api.SetHotkeysAsync(false, Ct);
        var pausedLights = await client.Api.GetPausedLightsAsync(Ct);
        await client.Api.SetPausedLightsAsync(PausedLightsMode.Off, Ct);
        var sceneFade = await client.Api.GetSceneFadeAsync(Ct);
        await client.Api.SetSceneFadeAsync(TimeSpan.FromSeconds(0.25), Ct);
        var customColors = await client.Api.GetCustomColorsAsync(Ct);
        await client.Api.SetCustomColorsAsync([new RgbColor(0, 0, 255), RgbColor.White], Ct);

        Assert.Equal(24, framesPerSecond);
        Assert.True(pauseOnLock);
        Assert.False(catchUp);
        Assert.True(hotkeys);
        Assert.Equal(PausedLightsMode.Dim, pausedLights);
        Assert.Equal(TimeSpan.FromSeconds(1.5), sceneFade);
        Assert.Equal([new RgbColor(255, 0, 0)], customColors);
        Assert.Equal(["fps 60", "pause on lock False", "catch up True", "hotkeys False", "paused lights Off", "scene fade 0.25", "custom colors #0000FF #FFFFFF"], _api.Calls);
    }

    // "Starten met Windows" crosses the pipe both ways, with whether Windows let it change.
    [Fact]
    public async Task AutoStart_CrossesThePipeWithItsAnswer()
    {
        var client = await ConnectAsync();

        var enabled = await client.Api.GetAutoStartAsync(Ct);
        var turnedOff = await client.Api.SetAutoStartAsync(false, Ct);
        _api.AutoStartChangeable = false;
        var turnedOn = await client.Api.SetAutoStartAsync(true, Ct);

        Assert.True(enabled);
        Assert.True(turnedOff);
        Assert.False(turnedOn);
        Assert.Equal(["autostart False", "autostart True"], _api.Calls);
    }

    // The steps go over the pipe with their colors and times, and a new effect comes back with its id.
    [Fact]
    public async Task CustomEffects_ReachServerAndBack()
    {
        var client = await ConnectAsync();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Api.CustomEffectsChanged += (_, _) => changed.TrySetResult();
        var effect = new CustomEffect("", "Feest", [new CustomEffectStep(new RgbColor(0, 255, 0), 1.5, 0), new CustomEffectStep([RgbColor.White, RgbColor.Black])]);
        _api.NewCustomEffectId = "custom:new";

        var effects = await client.Api.GetCustomEffectsAsync(Ct);
        var saved = await client.Api.SaveCustomEffectAsync(effect, Ct);
        await client.Api.DeleteCustomEffectAsync("custom:1", Ct);
        _api.RaiseCustomEffectsChanged();

        Assert.Equal([new CustomEffect("custom:1", "Disco", [new CustomEffectStep([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)], 0.5, 2)])], effects);
        Assert.Equal(effect with { Id = "custom:new" }, saved);
        Assert.Equal([effect with { Id = "custom:new" }], _api.CustomEffects);
        Assert.Equal(2, _api.Calls.Count);
        Assert.StartsWith("custom-effect save custom:new Feest ", _api.Calls[0]);
        Assert.Equal("custom-effect delete custom:1", _api.Calls[1]);
        await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task ServerError_ReachesClientAsRemoteInvocationException()
    {
        var client = await ConnectAsync();
        _api.FailOnce = ("effect all unknown", new ArgumentException("Unknown effect 'unknown'.", "effectId"));

        var exception = await Assert.ThrowsAsync<RemoteInvocationException>(
            () => client.Api.SetEffectAsync(null, "unknown", Ct));
        Assert.True(EngineRpc.IsRejectedByEngine(exception));
    }

    [Fact]
    public void IsRejectedByEngine_LostConnection_IsFalse()
    {
        Assert.False(EngineRpc.IsRejectedByEngine(new ConnectionLostException()));
    }

    [Fact]
    public async Task Disconnected_ServerStops_IsRaised()
    {
        var client = await ConnectAsync();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += (_, _) => disconnected.TrySetResult();

        _server!.Dispose();

        await disconnected.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task ConnectAsync_NoServer_ThrowsTimeout()
    {
        await Assert.ThrowsAsync<TimeoutException>(
            () => EngineConnection.ConnectAsync(_pipeName, TimeSpan.FromMilliseconds(100), Ct));
    }

    private async Task<EngineConnection> ConnectAsync()
    {
        var pipe = EngineRpc.CreateServerStream(_pipeName);
        var accepted = pipe.WaitForConnectionAsync(Ct);
        _client = await EngineConnection.ConnectAsync(_pipeName, TestTimeouts.Short, Ct);
        await accepted;
        _server = EngineRpc.StartServer(pipe, _api);
        return _client;
    }

    // What an engine before RescanDevicesWithOutcomeAsync offers for "Verversen".
    private sealed class OlderEngineApi
    {
        public Task<bool> RescanDevicesAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
