using BuurmansRgb.Core;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts.Tests;

public sealed class EngineRpcTests : IDisposable
{
    // 07:00 on workdays and 09:00 in the weekend.
    private static readonly ProfileAutomation MoreTimes = new([new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend)], "eldenring.exe");

    private readonly string _pipeName = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly FakeEngineApi _api = new();
    private JsonRpc? _server;
    private EngineConnection? _client;

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

        Assert.Equal([("fan", new RgbColor(255, 136, 0)), (null, RgbColor.White)], _api.ColorCalls);
    }

    [Fact]
    public async Task GetLightingAsync_RoundTripsEveryField()
    {
        var client = await ConnectAsync();

        var lighting = await client.Api.GetLightingAsync("fan", Ct);

        Assert.Equal(_api.Lighting, lighting);
    }

    [Fact]
    public async Task GetAllLightingAsync_GivesEveryDeviceWithEveryField()
    {
        var client = await ConnectAsync();

        var lighting = await client.Api.GetAllLightingAsync(Ct);

        Assert.Equal(_api.Devices.Select(device => device.Id), lighting.Select(device => device.DeviceId));
        Assert.All(lighting, device => Assert.Equal(_api.Lighting, device.Lighting));
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
        Assert.Equal(["save Werk", "load Gamen", "delete Nacht"], _api.ProfileCalls);
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

        Assert.Equal(_api.Lighting, await client.Api.GetProfileLightingAsync("Nacht", Ct));
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
            _api.AutomationCalls);
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

        Assert.Equal(1, _api.ResumeCalls);
    }

    [Fact]
    public async Task SetColorLoopAsync_ReachesServerWithColorsInOrder()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 255, 0), new(0, 255, 0), new(0, 0, 255)];

        await client.Api.SetColorLoopAsync("fan", colors, spread: false, Ct);

        var call = Assert.Single(_api.ColorLoopCalls);
        Assert.Equal("fan", call.DeviceId);
        Assert.Equal(colors, call.Colors);
        Assert.False(call.Spread);
    }

    [Fact]
    public async Task SetEffectColorsAsync_ReachesServerWithColorsInOrder()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(0, 0, 60), new(0, 0, 255), new(0, 200, 255)];

        await client.Api.SetEffectColorsAsync(null, EffectIds.Fire, colors, Ct);

        var call = Assert.Single(_api.EffectColorsCalls);
        Assert.Null(call.DeviceId);
        Assert.Equal(EffectIds.Fire, call.EffectId);
        Assert.Equal(colors, call.Colors);
    }

    [Fact]
    public async Task ClearEffectColorsAsync_ReachesServerWithDeviceAndEffect()
    {
        var client = await ConnectAsync();

        await client.Api.ClearEffectColorsAsync("fan", EffectIds.Comet, Ct);
        await client.Api.ClearEffectColorsAsync(null, EffectIds.Fire, Ct);

        Assert.Equal([("fan", EffectIds.Comet), (null, EffectIds.Fire)], _api.ClearEffectColorsCalls);
    }

    [Fact]
    public async Task SetOnAsync_ReachesServer()
    {
        var client = await ConnectAsync();

        await client.Api.SetOnAsync("fan", on: false, Ct);
        await client.Api.SetOnAsync(null, on: true, Ct);

        Assert.Equal([("fan", false), (null, true)], _api.OnCalls);
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

        Assert.Equal([(null, 2.5)], _api.SpeedCalls);
    }

    [Fact]
    public async Task LedColors_RoundTripThroughThePipe()
    {
        var client = await ConnectAsync();
        RgbColor[] colors = [new(255, 0, 0), new(0, 255, 0)];

        await client.Api.SetLedColorsAsync("fan", 3, colors, Ct);
        var stored = await client.Api.GetLedColorsAsync("fan", Ct);

        var call = Assert.Single(_api.LedColorCalls);
        Assert.Equal(("fan", 3), (call.DeviceId, call.FirstLed));
        Assert.Equal(colors, call.Colors);
        Assert.Equal(_api.LedColors, stored);
    }

    [Fact]
    public async Task ResizeZoneAsync_SendsDeviceZoneAndLength()
    {
        var client = await ConnectAsync();

        await client.Api.ResizeZoneAsync("board", 1, 24, Ct);

        Assert.Equal([("board", 1, 24)], _api.ResizeCalls);
    }

    [Fact]
    public async Task SetZonesAlikeAsync_SendsTheDeviceAndTheChoice()
    {
        var client = await ConnectAsync();

        await client.Api.SetZonesAlikeAsync("board", true, Ct);
        await client.Api.SetZonesAlikeAsync("strip", false, Ct);

        Assert.Equal([("board", true), ("strip", false)], _api.ZonesAlikeCalls);
    }

    [Fact]
    public async Task FlashDeviceAsync_SendsTheDevice()
    {
        var client = await ConnectAsync();

        await client.Api.FlashDeviceAsync("fan", Ct);

        Assert.Equal(["fan"], _api.FlashCalls);
    }

    [Fact]
    public async Task FlashZoneAsync_SendsTheDeviceAndZone()
    {
        var client = await ConnectAsync();

        await client.Api.FlashZoneAsync("board", 2, Ct);

        Assert.Equal([("board", 2)], _api.FlashZoneCalls);
        Assert.Empty(_api.FlashCalls);
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
                "start board 2 5",
                $"set board 2 {new ZoneCountChange(Led: 37)}",
                $"set board 2 {new ZoneCountChange(Running: false, LedsPerSecond: 2)}",
                "stop board 2",
            ],
            _api.ZoneCountCalls);
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

        Assert.False(await client.Api.RescanDevicesAsync(Ct));

        Assert.Equal(1, _api.RescanCalls);
    }

    [Fact]
    public async Task SimulatedDevices_ModeCrossesThePipeBothWays()
    {
        var client = await ConnectAsync();

        var mode = await client.Api.GetSimulatedDevicesAsync(Ct);
        await client.Api.SetSimulatedDevicesAsync(SimulatedDevicesMode.Never, Ct);

        Assert.Equal(SimulatedDevicesMode.Always, mode);
        Assert.Equal([SimulatedDevicesMode.Never], _api.SimulatedDevicesCalls);
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
        var customColors = await client.Api.GetCustomColorsAsync(Ct);
        await client.Api.SetCustomColorsAsync([new RgbColor(0, 0, 255), RgbColor.White], Ct);

        Assert.Equal(24, framesPerSecond);
        Assert.True(pauseOnLock);
        Assert.False(catchUp);
        Assert.True(hotkeys);
        Assert.Equal(PausedLightsMode.Dim, pausedLights);
        Assert.Equal([new RgbColor(255, 0, 0)], customColors);
        Assert.Equal(["fps 60", "pause on lock False", "catch up True", "hotkeys False", "paused lights Off", "custom colors #0000FF #FFFFFF"], _api.OptionCalls);
    }

    [Fact]
    public async Task ServerError_ReachesClientAsRemoteInvocationException()
    {
        var client = await ConnectAsync();

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

    private sealed class FakeEngineApi : IEngineApi
    {
        public event EventHandler<LightingFrame>? FrameRendered;

        public event EventHandler? DevicesChanged;

        public event EventHandler<SettingsSaveResult>? SettingsSaved;

        public event EventHandler<ServerStatus>? OpenRgbStatusChanged;

        public event EventHandler<PauseReasons>? PauseChanged;

        public event EventHandler? LightingChanged;

        public event EventHandler? ProfilesChanged;

        public event EventHandler? OptionsChanged;

        public event EventHandler<ZoneCountInfo>? ZoneCountChanged;

        public void RaiseProfilesChanged() => ProfilesChanged?.Invoke(this, EventArgs.Empty);

        public void RaiseOptionsChanged() => OptionsChanged?.Invoke(this, EventArgs.Empty);

        public IReadOnlyList<DeviceInfo> Devices { get; } =
        [
            new("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]) { Source = DeviceSource.Simulated },
            new("board", "Motherboard", DeviceType.Motherboard, 4, [RgbZone.Fixed("Mainboard", 4), new RgbZone("ARGB header", 0, 0, 120)])
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

        public LightingInfo Lighting { get; } = new(
            new RgbColor(10, 20, 30), 0.25, EffectIds.Static, 1.5, new ColorLoop([new RgbColor(1, 2, 3), new RgbColor(4, 5, 6)], Spread: false), On: false,
            EffectColors: EffectColors.None.With(EffectIds.Comet, [new RgbColor(7, 8, 9)]).With(EffectIds.Fire, [new RgbColor(1, 1, 1), new RgbColor(9, 9, 9)]));

        public List<(string? DeviceId, RgbColor Color)> ColorCalls { get; } = [];

        public IReadOnlyList<RgbColor> LedColors { get; } = [new(1, 1, 1), new(2, 2, 2)];

        public List<(string DeviceId, int FirstLed, IReadOnlyList<RgbColor> Colors)> LedColorCalls { get; } = [];

        public List<(string DeviceId, int ZoneIndex, int LedCount)> ResizeCalls { get; } = [];

        public void RaiseFrame(LightingFrame frame) => FrameRendered?.Invoke(this, frame);

        public List<(string? DeviceId, double Speed)> SpeedCalls { get; } = [];

        public List<(string? DeviceId, bool On)> OnCalls { get; } = [];

        public Task SetOnAsync(string? deviceId, bool on, CancellationToken ct = default)
        {
            OnCalls.Add((deviceId, on));
            return Task.CompletedTask;
        }

        public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

        public void RaiseSaved(bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

        public void RaiseOpenRgbStatus(ServerStatus status) => OpenRgbStatusChanged?.Invoke(this, status);

        public void RaisePause(PauseReasons pause) => PauseChanged?.Invoke(this, pause);

        public void RaiseLightingChanged() => LightingChanged?.Invoke(this, EventArgs.Empty);

        public int ResumeCalls { get; private set; }

        public Task<PauseReasons> GetPauseAsync(CancellationToken ct = default) => Task.FromResult(PauseReasons.Manual | PauseReasons.Locked);

        public Task ResumeEffectsAsync(CancellationToken ct = default)
        {
            ResumeCalls++;
            return Task.CompletedTask;
        }

        public Task<ServerStatus> GetOpenRgbStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServerStatus(ServerState.Connected, 4));

        public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) => Task.FromResult(Devices);

        public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([EffectIds.Static]);

        public Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default) => Task.FromResult(Lighting);

        public Task<IReadOnlyList<DeviceLightingInfo>> GetAllLightingAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceLightingInfo>>([.. Devices.Select(device => new DeviceLightingInfo(device.Id, Lighting))]);

        public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(LightingFrame.Empty);

        public List<bool> FramesWantedCalls { get; } = [];

        public Task SetFramesWantedAsync(bool wanted, CancellationToken ct = default)
        {
            FramesWantedCalls.Add(wanted);
            return Task.CompletedTask;
        }

        public List<IReadOnlyList<string>?> FrameDevicesCalls { get; } = [];

        public Task SetFrameDevicesAsync(IReadOnlyList<string>? deviceIds, CancellationToken ct = default)
        {
            FrameDevicesCalls.Add(deviceIds);
            return Task.CompletedTask;
        }

        public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default)
        {
            ColorCalls.Add((deviceId, color));
            return Task.CompletedTask;
        }

        public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default) => Task.CompletedTask;

        public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
            throw new ArgumentException($"Unknown effect '{effectId}'.", nameof(effectId));

        public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default)
        {
            SpeedCalls.Add((deviceId, speed));
            return Task.CompletedTask;
        }

        public List<(string? DeviceId, IReadOnlyList<RgbColor> Colors, bool Spread)> ColorLoopCalls { get; } = [];

        public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default)
        {
            ColorLoopCalls.Add((deviceId, colors, spread));
            return Task.CompletedTask;
        }

        public List<(string? DeviceId, string EffectId, IReadOnlyList<RgbColor> Colors)> EffectColorsCalls { get; } = [];

        public Task SetEffectColorsAsync(string? deviceId, string effectId, IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
        {
            EffectColorsCalls.Add((deviceId, effectId, colors));
            return Task.CompletedTask;
        }

        public List<(string? DeviceId, string EffectId)> ClearEffectColorsCalls { get; } = [];

        public Task ClearEffectColorsAsync(string? deviceId, string effectId, CancellationToken ct = default)
        {
            ClearEffectColorsCalls.Add((deviceId, effectId));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default) => Task.FromResult(LedColors);

        public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
        {
            LedColorCalls.Add((deviceId, firstLed, colors));
            return Task.CompletedTask;
        }

        public Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default)
        {
            ResizeCalls.Add((deviceId, zoneIndex, ledCount));
            return Task.CompletedTask;
        }

        public List<(string DeviceId, bool Alike)> ZonesAlikeCalls { get; } = [];

        public Task SetZonesAlikeAsync(string deviceId, bool alike, CancellationToken ct = default)
        {
            ZonesAlikeCalls.Add((deviceId, alike));
            return Task.CompletedTask;
        }

        public List<string> FlashCalls { get; } = [];

        public Task FlashDeviceAsync(string deviceId, CancellationToken ct = default)
        {
            FlashCalls.Add(deviceId);
            return Task.CompletedTask;
        }

        public List<(string DeviceId, int ZoneIndex)> FlashZoneCalls { get; } = [];

        public Task FlashZoneAsync(string deviceId, int zoneIndex, CancellationToken ct = default)
        {
            FlashZoneCalls.Add((deviceId, zoneIndex));
            return Task.CompletedTask;
        }

        public List<string> ZoneCountCalls { get; } = [];

        public Task StartZoneCountAsync(string deviceId, int zoneIndex, int ledsPerSecond, CancellationToken ct = default)
        {
            ZoneCountCalls.Add($"start {deviceId} {zoneIndex} {ledsPerSecond}");
            return Task.CompletedTask;
        }

        public Task SetZoneCountAsync(string deviceId, int zoneIndex, ZoneCountChange change, CancellationToken ct = default)
        {
            ZoneCountCalls.Add($"set {deviceId} {zoneIndex} {change}");
            return Task.CompletedTask;
        }

        public Task StopZoneCountAsync(string deviceId, int zoneIndex, CancellationToken ct = default)
        {
            ZoneCountCalls.Add($"stop {deviceId} {zoneIndex}");
            return Task.CompletedTask;
        }

        public Task<ZoneCountInfo> GetZoneCountAsync(CancellationToken ct = default) =>
            Task.FromResult(new ZoneCountInfo("board", 1, 37, 120, 2, true) { Sequence = 7 });

        public void RaiseZoneCount(ZoneCountInfo count) => ZoneCountChanged?.Invoke(this, count);

        public int RescanCalls { get; private set; }

        public Task<bool> RescanDevicesAsync(CancellationToken ct = default)
        {
            RescanCalls++;
            return Task.FromResult(false);
        }

        public List<SimulatedDevicesMode> SimulatedDevicesCalls { get; } = [];

        public Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default) =>
            Task.FromResult(SimulatedDevicesMode.Always);

        public Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default)
        {
            SimulatedDevicesCalls.Add(mode);
            return Task.CompletedTask;
        }

        public List<string> OptionCalls { get; } = [];

        public Task<int> GetFramesPerSecondAsync(CancellationToken ct = default) => Task.FromResult(24);

        public Task SetFramesPerSecondAsync(int framesPerSecond, CancellationToken ct = default)
        {
            OptionCalls.Add($"fps {framesPerSecond}");
            return Task.CompletedTask;
        }

        public Task<bool> GetPauseOnLockAndSleepAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task SetPauseOnLockAndSleepAsync(bool enabled, CancellationToken ct = default)
        {
            OptionCalls.Add($"pause on lock {enabled}");
            return Task.CompletedTask;
        }

        public Task<PausedLightsMode> GetPausedLightsAsync(CancellationToken ct = default) => Task.FromResult(PausedLightsMode.Dim);

        public Task SetPausedLightsAsync(PausedLightsMode mode, CancellationToken ct = default)
        {
            OptionCalls.Add($"paused lights {mode}");
            return Task.CompletedTask;
        }

        public Task<bool> GetCatchUpSceneTimesAsync(CancellationToken ct = default) => Task.FromResult(false);

        public Task SetCatchUpSceneTimesAsync(bool enabled, CancellationToken ct = default)
        {
            OptionCalls.Add($"catch up {enabled}");
            return Task.CompletedTask;
        }

        public Task<bool> GetHotkeysAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task SetHotkeysAsync(bool enabled, CancellationToken ct = default)
        {
            OptionCalls.Add($"hotkeys {enabled}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RgbColor>> GetCustomColorsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RgbColor>>([new RgbColor(255, 0, 0)]);

        public Task SetCustomColorsAsync(IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
        {
            OptionCalls.Add($"custom colors {string.Join(" ", colors.Select(color => color.ToHex()))}");
            return Task.CompletedTask;
        }

        public List<string> ProfileCalls { get; } = [];

        public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["Gamen", "Nacht"]);

        public Task SaveProfileAsync(string name, CancellationToken ct = default) => RecordProfile($"save {name}");

        public Task LoadProfileAsync(string name, CancellationToken ct = default) => RecordProfile($"load {name}");

        public Task DeleteProfileAsync(string name, CancellationToken ct = default) => RecordProfile($"delete {name}");

        public string? ActiveProfile { get; set; } = "Nacht";

        public Task<string?> GetActiveProfileAsync(CancellationToken ct = default) => Task.FromResult(ActiveProfile);

        public Task<LightingInfo> GetProfileLightingAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(name == "Nacht" ? Lighting : throw new ArgumentException($"There is no profile '{name}'.", nameof(name)));

        public List<(string Name, ProfileAutomation Automation)> AutomationCalls { get; } = [];

        public Task<ProfileAutomation> GetProfileAutomationAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(name == "Ochtend" ? MoreTimes : new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"));

        public Task SetProfileAutomationAsync(string name, ProfileAutomation automation, CancellationToken ct = default)
        {
            AutomationCalls.Add((name, automation));
            return Task.CompletedTask;
        }

        private Task RecordProfile(string call)
        {
            ProfileCalls.Add(call);
            return Task.CompletedTask;
        }
    }
}
