using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

internal sealed class FakeEngineConnector : IEngineConnector
{
    public Exception? ConnectException { get; set; }

    public FakeEngineConnection Connection { get; private set; } = new();

    public int ConnectCount { get; private set; }

    public Task<IEngineConnection> ConnectAsync(CancellationToken ct)
    {
        ConnectCount++;
        if (ConnectException is not null)
        {
            return Task.FromException<IEngineConnection>(ConnectException);
        }

        Connection = new FakeEngineConnection();
        return Task.FromResult<IEngineConnection>(Connection);
    }
}

internal sealed class FakeEngineConnection : IEngineConnection
{
    public FakeEngineApi FakeApi { get; } = new();

    public IEngineApi Api => FakeApi;

    public bool IsDisposed { get; private set; }

    public event EventHandler? Disconnected;

    public void Disconnect() => Disconnected?.Invoke(this, EventArgs.Empty);

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeEngineApi : IEngineApi
{
    public event EventHandler<LightingFrame>? FrameRendered;

    public event EventHandler? DevicesChanged;

    public event EventHandler<SettingsSaveResult>? SettingsSaved;

    public event EventHandler<ServerStatus>? OpenRgbStatusChanged;

    public ServerStatus OpenRgbStatus { get; set; } = new(ServerState.Connected, 2);

    public bool HasOpenRgbStatusSubscribers => OpenRgbStatusChanged is not null;

    public void RaiseOpenRgbStatus(ServerStatus status)
    {
        OpenRgbStatus = status;
        OpenRgbStatusChanged?.Invoke(this, status);
    }

    public PauseReasons Pause { get; set; }

    public bool HasPauseSubscribers => PauseChanged is not null;

    public event EventHandler<PauseReasons>? PauseChanged;

    public void RaisePause(PauseReasons pause)
    {
        Pause = pause;
        PauseChanged?.Invoke(this, pause);
    }

    public event EventHandler? LightingChanged;

    public event EventHandler? ProfilesChanged;

    public event EventHandler? OptionsChanged;

    public void RaiseProfilesChanged() => ProfilesChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseOptionsChanged() => OptionsChanged?.Invoke(this, EventArgs.Empty);

    public bool HasProfilesOrOptionsSubscribers => ProfilesChanged is not null || OptionsChanged is not null;

    public bool HasLightingSubscribers => LightingChanged is not null;

    public void RaiseLightingChanged() => LightingChanged?.Invoke(this, EventArgs.Empty);

    // When set, the pause changes to this while GetPauseAsync answers with the older one, as when
    // the event overtakes the answer.
    public PauseReasons? PauseChangeWhileAnswering { get; set; }

    public Task<PauseReasons> GetPauseAsync(CancellationToken ct = default)
    {
        var answer = Pause;
        if (PauseChangeWhileAnswering is { } newer)
        {
            RaisePause(newer);
        }

        return Task.FromResult(answer);
    }

    // Like the engine: ends the manual pause only, and reports the new pause.
    public async Task ResumeEffectsAsync(CancellationToken ct = default)
    {
        await Record("resume");
        RaisePause(Pause & ~PauseReasons.Manual);
    }

    // When set, the status changes to this while GetOpenRgbStatusAsync answers with the older one,
    // as when the event overtakes the answer.
    public ServerStatus? StatusChangeWhileAnswering { get; set; }

    public Task<ServerStatus> GetOpenRgbStatusAsync(CancellationToken ct = default)
    {
        var answer = OpenRgbStatus;
        if (StatusChangeWhileAnswering is { } newer)
        {
            RaiseOpenRgbStatus(newer);
        }

        return Task.FromResult(answer);
    }

    public IReadOnlyList<DeviceInfo> Devices { get; set; } =
    [
        TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 3),
        TestDevices.Create("fan", "Fan", DeviceType.Fan, 2),
    ];

    public Dictionary<string, LightingInfo> Lighting { get; } = new()
    {
        [""] = new(new RgbColor(255, 0, 0), 0.8, EffectIds.Static),
        ["fan"] = new(new RgbColor(0, 0, 255), 0.5, EffectIds.Static),
    };

    public Dictionary<string, RgbColor[]> LedColors { get; } = [];

    public LightingFrame Frame { get; set; } = LightingFrame.Empty;

    public List<string> Calls { get; } = [];

    public Exception? SetException { get; set; }

    public bool HasFrameSubscribers => FrameRendered is not null;

    public void RaiseFrame(LightingFrame frame) => FrameRendered?.Invoke(this, frame);

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseSaved(bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) => Task.FromResult(Devices);

    public IReadOnlyList<string> Effects { get; set; } = [EffectIds.Static, EffectIds.PerLed];

    public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) => Task.FromResult(Effects);

    // When set, GetLightingAsync answers once this task completes, like a slow engine.
    public Task? LightingGate { get; set; }

    public async Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default)
    {
        if (LightingGate is { } gate)
        {
            await gate;
        }

        return Lighting.GetValueOrDefault(deviceId ?? "", Lighting[""]);
    }

    // Like the engine: every device, with its own lighting or else that of all devices.
    public int AllLightingCalls { get; private set; }

    public Task<IReadOnlyList<DeviceLightingInfo>> GetAllLightingAsync(CancellationToken ct = default)
    {
        AllLightingCalls++;
        return Task.FromResult<IReadOnlyList<DeviceLightingInfo>>([.. Devices.Select(device => new DeviceLightingInfo(device.Id, Lighting.GetValueOrDefault(device.Id, Lighting[""])))]);
    }

    public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(Frame);

    // What the app said last; null while it said nothing, so the engine sends frames.
    public bool? FramesWanted { get; private set; }

    public Task SetFramesWantedAsync(bool wanted, CancellationToken ct = default)
    {
        FramesWanted = wanted;
        return Task.CompletedTask;
    }

    // Null for all devices, as in the engine.
    public IReadOnlyList<string>? FrameDevices { get; private set; }

    public List<IReadOnlyList<string>?> FrameDevicesCalls { get; } = [];

    public Task SetFrameDevicesAsync(IReadOnlyList<string>? deviceIds, CancellationToken ct = default)
    {
        FrameDevices = deviceIds;
        FrameDevicesCalls.Add(deviceIds);
        return Task.CompletedTask;
    }

    public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default) =>
        Record($"color {deviceId ?? "all"} {color.ToHex()}");

    public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"brightness {deviceId ?? "all"} {brightness:0.00}"));

    public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
        Record($"effect {deviceId ?? "all"} {effectId}");

    public Task SetOnAsync(string? deviceId, bool on, CancellationToken ct = default) =>
        Record($"on {deviceId ?? "all"} {(on ? "on" : "off")}");

    public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"speed {deviceId ?? "all"} {speed:0.00}"));

    public Task SetDirectionAsync(string? deviceId, int? degrees, CancellationToken ct = default) =>
        Record($"direction {deviceId ?? "all"} {(degrees is { } value ? value.ToString(CultureInfo.InvariantCulture) : "leds")}");

    public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default) =>
        Record($"loop {deviceId ?? "all"} {string.Join(",", colors.Select(color => color.ToHex()))} {(spread ? "spread" : "together")}");

    public Task SetEffectColorsAsync(string? deviceId, string effectId, IReadOnlyList<RgbColor> colors, CancellationToken ct = default) =>
        Record($"effect-colors {deviceId ?? "all"} {effectId} {string.Join(",", colors.Select(color => color.ToHex()))}");

    public Task ClearEffectColorsAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
        Record($"effect-colors {deviceId ?? "all"} {effectId} default");

    // When set, GetLedColorsAsync answers once this task completes, like a slow engine.
    public Task? LedColorsGate { get; set; }

    public int LedColorsCalls { get; private set; }

    // Like the engine: a device without stored colors has its own color on every LED.
    public async Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default)
    {
        LedColorsCalls++;
        if (LedColorsGate is { } gate)
        {
            await gate;
        }

        return LedColors.GetValueOrDefault(deviceId)
            ?? [.. Enumerable.Repeat(Lighting.GetValueOrDefault(deviceId, Lighting[""]).Color, Devices.Single(device => device.Id == deviceId).LedCount)];
    }

    public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default) =>
        Record($"leds {deviceId} {firstLed} {string.Join(",", colors.Select(color => color.ToHex()))}");

    // Like the engine: a count of the zone ends with this length.
    public async Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default)
    {
        await Record($"resize {deviceId} {zoneIndex} {ledCount}");
        if (ZoneCount.Counts(deviceId, zoneIndex))
        {
            RaiseZoneCount(ZoneCountInfo.None);
        }
    }

    public ZoneCountInfo ZoneCount { get; set; } = ZoneCountInfo.None;

    public event EventHandler<ZoneCountInfo>? ZoneCountChanged;

    public bool HasZoneCountSubscribers => ZoneCountChanged is not null;

    public void RaiseZoneCount(ZoneCountInfo count)
    {
        ZoneCount = count;
        ZoneCountChanged?.Invoke(this, count);
    }

    public Task<ZoneCountInfo> GetZoneCountAsync(CancellationToken ct = default) => Task.FromResult(ZoneCount);

    // Like the engine: the count starts at LED 1 and goes up to the longest length of the zone.
    public async Task StartZoneCountAsync(string deviceId, int zoneIndex, int ledsPerSecond, CancellationToken ct = default)
    {
        await Record($"count {deviceId} {zoneIndex} {ledsPerSecond}");
        var zone = Devices.Single(device => device.Id == deviceId).Zones[zoneIndex];
        RaiseZoneCount(new ZoneCountInfo(deviceId, zoneIndex, 1, zone.MaxLedCount, ledsPerSecond, true));
    }

    public async Task SetZoneCountAsync(string deviceId, int zoneIndex, ZoneCountChange change, CancellationToken ct = default)
    {
        var values = new[]
        {
            change.Led is { } led ? $"led {led}" : null,
            change.Running is { } running ? (running ? "run" : "pause") : null,
            change.LedsPerSecond is { } pace ? $"pace {pace}" : null,
        };
        await Record($"count {deviceId} {zoneIndex} {string.Join(' ', values.OfType<string>())}");
        if (ZoneCount.Counts(deviceId, zoneIndex))
        {
            var count = ZoneCount with
            {
                Led = Math.Clamp(change.Led ?? ZoneCount.Led, 1, ZoneCount.LedCount),
                Running = change.Running ?? ZoneCount.Running,
                LedsPerSecond = change.LedsPerSecond ?? ZoneCount.LedsPerSecond,
            };
            RaiseZoneCount(count);
        }
    }

    public async Task StopZoneCountAsync(string deviceId, int zoneIndex, CancellationToken ct = default)
    {
        await Record($"count {deviceId} {zoneIndex} stop");
        if (ZoneCount.Counts(deviceId, zoneIndex))
        {
            RaiseZoneCount(ZoneCountInfo.None);
        }
    }

    // Like the engine: the device comes again with the new choice, through DevicesChanged.
    public async Task SetZonesAlikeAsync(string deviceId, bool alike, CancellationToken ct = default)
    {
        await Record($"zones-alike {deviceId} {(alike ? "on" : "off")}");
        Devices = [.. Devices.Select(device => device.Id == deviceId ? device with { ZonesAlike = alike } : device)];
        RaiseDevicesChanged();
    }

    public Task FlashDeviceAsync(string deviceId, CancellationToken ct = default) => Record($"flash {deviceId}");

    public Task FlashZoneAsync(string deviceId, int zoneIndex, CancellationToken ct = default) => Record($"flash {deviceId} zone {zoneIndex}");

    // When set, a rescan answers once this task completes, like an engine that waits for OpenRGB.
    public Task? RescanGate { get; set; }

    // What a rescan answers: false as if OpenRGB could not be reached.
    public bool RescanReaches { get; set; } = true;

    // Like the engine: the new list goes out through DevicesChanged before the answer.
    public async Task<bool> RescanDevicesAsync(CancellationToken ct = default)
    {
        await Record("rescan");
        if (RescanGate is { } gate)
        {
            await gate;
        }

        RaiseDevicesChanged();
        return RescanReaches;
    }

    public SimulatedDevicesMode SimulatedDevices { get; set; } = SimulatedDevicesMode.Automatic;

    public Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default) => Task.FromResult(SimulatedDevices);

    public async Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default)
    {
        await Record($"simulated {mode}");
        SimulatedDevices = mode;
    }

    public int FramesPerSecond { get; set; } = 30;

    public Task<int> GetFramesPerSecondAsync(CancellationToken ct = default) => Task.FromResult(FramesPerSecond);

    public async Task SetFramesPerSecondAsync(int framesPerSecond, CancellationToken ct = default)
    {
        await Record($"fps {framesPerSecond}");
        FramesPerSecond = framesPerSecond;
    }

    public bool PauseOnLockAndSleep { get; set; } = true;

    public Task<bool> GetPauseOnLockAndSleepAsync(CancellationToken ct = default) => Task.FromResult(PauseOnLockAndSleep);

    public async Task SetPauseOnLockAndSleepAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"pause on lock {enabled}");
        PauseOnLockAndSleep = enabled;
    }

    public PausedLightsMode PausedLights { get; set; }

    public Task<PausedLightsMode> GetPausedLightsAsync(CancellationToken ct = default) => Task.FromResult(PausedLights);

    public async Task SetPausedLightsAsync(PausedLightsMode mode, CancellationToken ct = default)
    {
        await Record($"paused lights {mode}");
        PausedLights = mode;
    }

    public bool CatchUpSceneTimes { get; set; }

    public Task<bool> GetCatchUpSceneTimesAsync(CancellationToken ct = default) => Task.FromResult(CatchUpSceneTimes);

    public async Task SetCatchUpSceneTimesAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"catch up {enabled}");
        CatchUpSceneTimes = enabled;
    }

    public bool Hotkeys { get; set; } = true;

    public Task<bool> GetHotkeysAsync(CancellationToken ct = default) => Task.FromResult(Hotkeys);

    public async Task SetHotkeysAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"hotkeys {enabled}");
        Hotkeys = enabled;
    }

    // Off, as the engine in a test has it; never the registry of this pc.
    public bool AutoStart { get; set; }

    // False lets SetAutoStartAsync fail as when Windows does not let it change.
    public bool AutoStartChangeable { get; set; } = true;

    public Task<bool> GetAutoStartAsync(CancellationToken ct = default) => Task.FromResult(AutoStart);

    public async Task<bool> SetAutoStartAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"autostart {enabled}");
        if (!AutoStartChangeable)
        {
            return false;
        }

        AutoStart = enabled;
        return true;
    }

    public List<RgbColor> CustomColors { get; set; } = [];

    public Task<IReadOnlyList<RgbColor>> GetCustomColorsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RgbColor>>([.. CustomColors]);

    public async Task SetCustomColorsAsync(IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
    {
        await Record($"custom colors {string.Join(" ", colors.Select(color => color.ToHex()))}");
        CustomColors = [.. colors];
    }

    public event EventHandler? CustomEffectsChanged;

    public void RaiseCustomEffectsChanged() => CustomEffectsChanged?.Invoke(this, EventArgs.Empty);

    public bool HasCustomEffectsSubscribers => CustomEffectsChanged is not null;

    // Like the engine: in alphabetical order of their names; a new one gets the next id.
    public List<CustomEffect> CustomEffects { get; set; } = [];

    private int _nextCustomEffect = 1;

    // Such as the engine that cannot read effects.json yet.
    public Exception? GetCustomEffectsException { get; set; }

    public Task<IReadOnlyList<CustomEffect>> GetCustomEffectsAsync(CancellationToken ct = default) =>
        GetCustomEffectsException is { } exception
            ? Task.FromException<IReadOnlyList<CustomEffect>>(exception)
            : Task.FromResult<IReadOnlyList<CustomEffect>>([.. CustomEffects]);

    public async Task<CustomEffect> SaveCustomEffectAsync(CustomEffect effect, CancellationToken ct = default)
    {
        var saved = effect.Id.Length == 0 ? effect with { Id = $"custom:{_nextCustomEffect++}" } : effect;
        await Record($"custom-effect save {saved.Id} {saved.Name} {string.Join(" ", saved.Steps.Select(Describe))}");
        CustomEffects = [.. CustomEffects.Where(other => other.Id != saved.Id).Append(saved).OrderBy(other => other.Name, StringComparer.CurrentCultureIgnoreCase)];
        return saved;
    }

    public async Task DeleteCustomEffectAsync(string id, CancellationToken ct = default)
    {
        await Record($"custom-effect delete {id}");
        CustomEffects.RemoveAll(effect => effect.Id == id);
    }

    // "#FF0000/1/0" for a step of one color, "3leds/1/0" for a pattern.
    public static string Describe(CustomEffectStep step) => string.Create(
        CultureInfo.InvariantCulture,
        $"{(step.IsPattern ? $"{step.Colors.Count}leds" : step.Colors[0].ToHex())}/{step.HoldSeconds}/{step.FadeSeconds}");

    // Like the engine: saving adds the name, deleting removes it, in alphabetical order.
    public List<string> Profiles { get; } = [];

    // The lighting a loaded profile shows, by profile name.
    public Dictionary<string, LightingInfo> ProfileLighting { get; } = [];

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Profiles]);

    // Like the engine: the profile saved or loaded last; tests set it for a scene from the tray.
    public string? ActiveProfile { get; set; }

    public Task<string?> GetActiveProfileAsync(CancellationToken ct = default) => Task.FromResult(ActiveProfile);

    public async Task SaveProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile save {name}");
        Profiles.Add(name);
        Profiles.Sort(StringComparer.CurrentCultureIgnoreCase);
        ActiveProfile = name;
    }

    public async Task LoadProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile load {name}");
        if (ProfileLighting.TryGetValue(name, out var lighting))
        {
            Lighting.Clear();
            Lighting[""] = lighting;
        }

        ActiveProfile = name;
    }

    public async Task DeleteProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile delete {name}");
        Profiles.Remove(name);
        if (ActiveProfile == name)
        {
            ActiveProfile = null;
        }
    }

    // Profiles that are gone by the time they are asked, such as deleted from the tray menu.
    public HashSet<string> GoneProfiles { get; } = [];

    // Like the engine: the lighting a profile gives all devices; the default lighting without one.
    public Task<LightingInfo> GetProfileLightingAsync(string name, CancellationToken ct = default) =>
        GoneProfiles.Contains(name)
            ? Task.FromException<LightingInfo>(new ArgumentException($"There is no profile '{name}'.", nameof(name)))
            : Task.FromResult(ProfileLighting.GetValueOrDefault(name, new LightingInfo(RgbColor.White, 1.0, EffectIds.Static)));

    // By profile name; a profile without one never loads by itself.
    public Dictionary<string, ProfileAutomation> ProfileAutomations { get; } = [];

    public Task<ProfileAutomation> GetProfileAutomationAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(ProfileAutomations.GetValueOrDefault(name, ProfileAutomation.None));

    // Like the engine: saved as Normalize gives it.
    public async Task SetProfileAutomationAsync(string name, ProfileAutomation automation, CancellationToken ct = default)
    {
        await Record($"profile automation {name} {(automation.Times.Count == 0 ? "-" : string.Join(",", automation.Times.Select(time => time.At.ToString("HH:mm", CultureInfo.InvariantCulture))))} {automation.Program ?? "-"}");
        ProfileAutomations[name] = automation.Normalize();
    }

    // When set, a call that changes something counts once this task completes, like a busy engine,
    // so the calls after it wait in the queue of the app.
    public Task? SetGate { get; set; }

    private Task Record(string call) => SetGate is { } gate ? RecordAfterAsync(gate, call) : RecordNow(call);

    private async Task RecordAfterAsync(Task gate, string call)
    {
        await gate;
        await RecordNow(call);
    }

    private Task RecordNow(string call)
    {
        if (SetException is not null)
        {
            return Task.FromException(SetException);
        }

        Calls.Add(call);
        return Task.CompletedTask;
    }
}
