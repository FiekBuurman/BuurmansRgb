using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Ipc;

// One instance per connected UI, so disposing it stops the frames for that UI only.
// LightingController checks the input; its ArgumentException reaches the UI as an RPC error.
internal sealed class EngineApi : IEngineApi, IDisposable
{
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _zoneLengths;
    private readonly ProfileLibrary _profiles;
    private readonly SimulatedDevicesSwitch _simulatedDevices;
    private readonly IServerStatusSource _openRgb;
    private readonly SettingsSaveEvents? _saveEvents;
    private readonly AutoPause? _autoPause;
    private readonly ProfileTimer? _profileTimer;
    private readonly AllOnOffHotkeys? _hotkeys;
    private readonly CustomColorList? _customColors;
    private readonly ZoneCounter? _zoneCounter;
    private readonly CustomEffectLibrary? _customEffects;
    private readonly PreviewFrames _frames;

    // The devices the app shows, or null for all; replaced as a whole, read by the frame timer.
    private volatile IReadOnlySet<string>? _frameDevices;

    public EngineApi(
        LightingController lighting,
        ZoneLengthKeeper zoneLengths,
        ProfileLibrary profiles,
        SimulatedDevicesSwitch simulatedDevices,
        IServerStatusSource openRgb,
        SettingsSaveEvents? saveEvents = null,
        TimeProvider? time = null,
        AutoPause? autoPause = null,
        ProfileTimer? profileTimer = null,
        AllOnOffHotkeys? hotkeys = null,
        CustomColorList? customColors = null,
        ZoneCounter? zoneCounter = null,
        CustomEffectLibrary? customEffects = null)
    {
        _zoneCounter = zoneCounter;
        _customEffects = customEffects;
        _autoPause = autoPause;
        _profileTimer = profileTimer;
        _hotkeys = hotkeys;
        _customColors = customColors;
        _lighting = lighting;
        _zoneLengths = zoneLengths;
        _profiles = profiles;
        _simulatedDevices = simulatedDevices;
        _openRgb = openRgb;
        _saveEvents = saveEvents;
        _frames = new PreviewFrames(_lighting.GetOutput, output => FrameRendered?.Invoke(this, ToFrame(output, _frameDevices)), time ?? TimeProvider.System);
        _lighting.Rendered += OnRendered;
        _lighting.DevicesChanged += OnDevicesChanged;
        _lighting.ZonesAlikeChanged += OnDevicesChanged;
        _openRgb.StatusChanged += OnOpenRgbStatusChanged;
        _lighting.PauseChanged += OnPauseChanged;
        _lighting.LightingChanged += OnLightingChanged;
        _saveEvents?.Saved += OnSaved;
        _profiles.Changed += OnProfilesChanged;
        _simulatedDevices.ModeChanged += OnOptionsChanged;
        _lighting.FramesPerSecondChanged += OnOptionsChanged;
        _lighting.PausedLightsChanged += OnOptionsChanged;
        _autoPause?.EnabledChanged += OnOptionsChanged;
        _profileTimer?.CatchUpChanged += OnOptionsChanged;
        _hotkeys?.EnabledChanged += OnOptionsChanged;
        _customColors?.Changed += OnOptionsChanged;
        _lighting.CountChanged += OnCountChanged;
        _customEffects?.Changed += OnCustomEffectsChanged;
    }

    public event EventHandler<LightingFrame>? FrameRendered;

    public event EventHandler? DevicesChanged;

    public event EventHandler<SettingsSaveResult>? SettingsSaved;

    public event EventHandler<ServerStatus>? OpenRgbStatusChanged;

    public event EventHandler<PauseReasons>? PauseChanged;

    public event EventHandler? LightingChanged;

    public event EventHandler? ProfilesChanged;

    public event EventHandler? OptionsChanged;

    public event EventHandler<ZoneCountInfo>? ZoneCountChanged;

    public event EventHandler? CustomEffectsChanged;

    // Zones alike is read after the devices; when it changes in between, DevicesChanged follows.
    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        var devices = _lighting.Devices;
        var zonesAlike = _lighting.ZonesAlike;
        return Task.FromResult<IReadOnlyList<DeviceInfo>>([.. devices.Select(device => DeviceInfo.From(device, zonesAlike.Contains(device.Id)))]);
    }

    public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) => Task.FromResult(EffectCatalog.Ids);

    // Without the library (in tests) there are none, and none can be saved.
    public Task<IReadOnlyList<CustomEffect>> GetCustomEffectsAsync(CancellationToken ct = default) =>
        Task.FromResult(_customEffects?.Effects ?? []);

    public Task<CustomEffect> SaveCustomEffectAsync(CustomEffect effect, CancellationToken ct = default) =>
        Task.FromResult(CustomEffectsOrThrow().Save(effect));

    public Task DeleteCustomEffectAsync(string id, CancellationToken ct = default)
    {
        CustomEffectsOrThrow().Delete(id);
        return Task.CompletedTask;
    }

    public Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default) =>
        Task.FromResult(ToInfo(_lighting.GetLighting(deviceId)));

    public Task<IReadOnlyList<DeviceLightingInfo>> GetAllLightingAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DeviceLightingInfo>>([.. _lighting.GetAllLighting().Select(device => new DeviceLightingInfo(device.DeviceId, ToInfo(device.Lighting)))]);

    public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(ToFrame(_lighting.GetOutput()));

    public Task SetFramesWantedAsync(bool wanted, CancellationToken ct = default)
    {
        _frames.Wanted = wanted;
        return Task.CompletedTask;
    }

    public Task SetFrameDevicesAsync(IReadOnlyList<string>? deviceIds, CancellationToken ct = default)
    {
        _frameDevices = deviceIds is null ? null : new HashSet<string>(deviceIds, StringComparer.Ordinal);
        return Task.CompletedTask;
    }

    public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Color = color });
        return Task.CompletedTask;
    }

    public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Brightness = brightness });
        return Task.CompletedTask;
    }

    public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { EffectId = effectId });
        return Task.CompletedTask;
    }

    public Task SetOnAsync(string? deviceId, bool on, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { On = on });
        return Task.CompletedTask;
    }

    public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Speed = speed });
        return Task.CompletedTask;
    }

    public Task SetDirectionAsync(string? deviceId, int? degrees, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Direction = degrees });
        return Task.CompletedTask;
    }

    // A copy of the colors, so the caller cannot change the loop afterwards.
    public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var loop = new ColorLoop([.. colors], spread);
        _lighting.SetLighting(deviceId, lighting => lighting with { Loop = loop });
        return Task.CompletedTask;
    }

    // With copies the colors, so the caller cannot change them afterwards either.
    public Task SetEffectColorsAsync(string? deviceId, string effectId, IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ThrowIfWithoutEffectColors(effectId);
        _lighting.SetLighting(deviceId, lighting => lighting with { EffectColors = lighting.EffectColors.With(effectId, colors) });
        return Task.CompletedTask;
    }

    public Task ClearEffectColorsAsync(string? deviceId, string effectId, CancellationToken ct = default)
    {
        ThrowIfWithoutEffectColors(effectId);
        _lighting.SetLighting(deviceId, lighting => lighting with { EffectColors = lighting.EffectColors.Without(effectId) });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default) =>
        Task.FromResult(_lighting.GetLedColors(deviceId));

    public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(colors);
        _lighting.SetLedColors(deviceId, firstLed, colors);
        return Task.CompletedTask;
    }

    // Through the counter when there is one, so a count of the zone ends with this length.
    public Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default)
    {
        if (_zoneCounter is { } counter)
        {
            counter.ResizeZone(deviceId, zoneIndex, ledCount);
        }
        else
        {
            _zoneLengths.ResizeZone(deviceId, zoneIndex, ledCount);
        }

        return Task.CompletedTask;
    }

    public Task SetZonesAlikeAsync(string deviceId, bool alike, CancellationToken ct = default)
    {
        _lighting.SetZonesAlike(deviceId, alike);
        return Task.CompletedTask;
    }

    public Task FlashDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        _lighting.Flash(deviceId);
        return Task.CompletedTask;
    }

    public Task FlashZoneAsync(string deviceId, int zoneIndex, CancellationToken ct = default)
    {
        _lighting.FlashZone(deviceId, zoneIndex);
        return Task.CompletedTask;
    }

    // This UI is the owner of the count it starts, so the count stops when it goes. Without the
    // counter (in tests) nothing is counted.
    public Task StartZoneCountAsync(string deviceId, int zoneIndex, int ledsPerSecond, CancellationToken ct = default)
    {
        _zoneCounter?.Start(deviceId, zoneIndex, ledsPerSecond, owner: this);
        return Task.CompletedTask;
    }

    public Task SetZoneCountAsync(string deviceId, int zoneIndex, ZoneCountChange change, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        _zoneCounter?.Change(deviceId, zoneIndex, change);
        return Task.CompletedTask;
    }

    public Task StopZoneCountAsync(string deviceId, int zoneIndex, CancellationToken ct = default)
    {
        _zoneCounter?.Stop(deviceId, zoneIndex);
        return Task.CompletedTask;
    }

    public Task<ZoneCountInfo> GetZoneCountAsync(CancellationToken ct = default) => Task.FromResult(_lighting.Count);

    // A count stops first and gives its zone its length back, so the new list has the zone as the
    // user knows it.
    public Task<bool> RescanDevicesAsync(CancellationToken ct = default)
    {
        _zoneCounter?.StopAll();
        return _lighting.RescanDevicesAsync(ct);
    }

    public Task<ServerStatus> GetOpenRgbStatusAsync(CancellationToken ct = default) => Task.FromResult(_openRgb.Status);

    public Task<PauseReasons> GetPauseAsync(CancellationToken ct = default) => Task.FromResult(_lighting.Pause);

    public Task ResumeEffectsAsync(CancellationToken ct = default)
    {
        _lighting.SetPaused(PauseReasons.Manual, false);
        return Task.CompletedTask;
    }

    public Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default) => Task.FromResult(_simulatedDevices.Mode);

    public Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default)
    {
        _simulatedDevices.Mode = mode;
        return Task.CompletedTask;
    }

    public Task<int> GetFramesPerSecondAsync(CancellationToken ct = default) => Task.FromResult(_lighting.FramesPerSecond);

    public Task SetFramesPerSecondAsync(int framesPerSecond, CancellationToken ct = default)
    {
        _lighting.FramesPerSecond = framesPerSecond;
        return Task.CompletedTask;
    }

    // Without AutoPause (in tests) there is nothing to pause, so it reads as off.
    public Task<bool> GetPauseOnLockAndSleepAsync(CancellationToken ct = default) => Task.FromResult(_autoPause?.Enabled ?? false);

    public Task SetPauseOnLockAndSleepAsync(bool enabled, CancellationToken ct = default)
    {
        _autoPause?.Enabled = enabled;
        return Task.CompletedTask;
    }

    public Task<PausedLightsMode> GetPausedLightsAsync(CancellationToken ct = default) => Task.FromResult(_lighting.PausedLights);

    // A mode this version does not know, from a newer settings app, keeps the last frame.
    public Task SetPausedLightsAsync(PausedLightsMode mode, CancellationToken ct = default)
    {
        _lighting.PausedLights = Enum.IsDefined(mode) ? mode : PausedLightsMode.Keep;
        return Task.CompletedTask;
    }

    // Without the timer (in tests) no scene loads at its time, so it reads as off.
    public Task<bool> GetCatchUpSceneTimesAsync(CancellationToken ct = default) => Task.FromResult(_profileTimer?.CatchUp ?? false);

    public Task SetCatchUpSceneTimesAsync(bool enabled, CancellationToken ct = default)
    {
        _profileTimer?.CatchUp = enabled;
        return Task.CompletedTask;
    }

    // Without the hotkeys (in tests) no key does anything, so they read as off. The tray registers
    // or frees the keys when they change.
    public Task<bool> GetHotkeysAsync(CancellationToken ct = default) => Task.FromResult(_hotkeys?.Enabled ?? false);

    public Task SetHotkeysAsync(bool enabled, CancellationToken ct = default)
    {
        _hotkeys?.Enabled = enabled;
        return Task.CompletedTask;
    }

    // Without the list (in tests) nothing is kept, so there are none.
    public Task<IReadOnlyList<RgbColor>> GetCustomColorsAsync(CancellationToken ct = default) =>
        Task.FromResult(_customColors?.Colors ?? []);

    public Task SetCustomColorsAsync(IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count > CustomColors.MaxColors)
        {
            throw new ArgumentException($"At most {CustomColors.MaxColors} own colors, not {colors.Count}.", nameof(colors));
        }

        _customColors?.Colors = colors;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) => Task.FromResult(_profiles.Names);

    public Task SaveProfileAsync(string name, CancellationToken ct = default)
    {
        _profiles.Save(name);
        return Task.CompletedTask;
    }

    public Task LoadProfileAsync(string name, CancellationToken ct = default)
    {
        _profiles.Load(name);
        return Task.CompletedTask;
    }

    public Task DeleteProfileAsync(string name, CancellationToken ct = default)
    {
        _profiles.Delete(name);
        return Task.CompletedTask;
    }

    public Task<string?> GetActiveProfileAsync(CancellationToken ct = default) => Task.FromResult(_profiles.ActiveName);

    public Task<LightingInfo> GetProfileLightingAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(ToInfo(_profiles.GetLighting(name).Global));

    public Task<ProfileAutomation> GetProfileAutomationAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(_profiles.GetAutomation(name));

    public Task SetProfileAutomationAsync(string name, ProfileAutomation automation, CancellationToken ct = default)
    {
        _profiles.SetAutomation(name, automation);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _lighting.Rendered -= OnRendered;
        _lighting.DevicesChanged -= OnDevicesChanged;
        _lighting.ZonesAlikeChanged -= OnDevicesChanged;
        _openRgb.StatusChanged -= OnOpenRgbStatusChanged;
        _lighting.PauseChanged -= OnPauseChanged;
        _lighting.LightingChanged -= OnLightingChanged;
        _saveEvents?.Saved -= OnSaved;
        _profiles.Changed -= OnProfilesChanged;
        _simulatedDevices.ModeChanged -= OnOptionsChanged;
        _lighting.FramesPerSecondChanged -= OnOptionsChanged;
        _lighting.PausedLightsChanged -= OnOptionsChanged;
        _autoPause?.EnabledChanged -= OnOptionsChanged;
        _profileTimer?.CatchUpChanged -= OnOptionsChanged;
        _hotkeys?.EnabledChanged -= OnOptionsChanged;
        _customColors?.Changed -= OnOptionsChanged;
        _lighting.CountChanged -= OnCountChanged;
        _customEffects?.Changed -= OnCustomEffectsChanged;
        _frames.Dispose();

        // The settings app that counted went, so nobody can stop the count any more.
        _zoneCounter?.StopFor(this);
    }

    private static void ThrowIfWithoutEffectColors(string effectId)
    {
        if (!EffectColors.Takes(effectId))
        {
            throw new ArgumentException($"The effect '{effectId}' has no colors of its own.", nameof(effectId));
        }
    }

    // The wire formats are made here, so the lighting model does not know them, and outside the
    // controller's lock.
    private static LightingInfo ToInfo(DeviceLighting lighting) =>
        new(lighting.Color, lighting.Brightness, lighting.EffectId, lighting.Speed, lighting.Loop, lighting.On, lighting.EffectColors, lighting.Direction);

    private static LightingFrame ToFrame(LightingOutput output, IReadOnlySet<string>? devices = null) =>
        new(output.Sequence, [.. output.Devices
            .Where(device => devices is null || devices.Contains(device.DeviceId))
            .Select(device => DeviceFrame.FromColors(device.DeviceId, device.Colors))]);

    private void OnDevicesChanged(object? sender, EventArgs e) => DevicesChanged?.Invoke(this, EventArgs.Empty);

    private void OnSaved(object? sender, bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

    private void OnOpenRgbStatusChanged(object? sender, ServerStatus status) => OpenRgbStatusChanged?.Invoke(this, status);

    private void OnPauseChanged(object? sender, PauseReasons pause) => PauseChanged?.Invoke(this, pause);

    private void OnLightingChanged(object? sender, EventArgs e) => LightingChanged?.Invoke(this, EventArgs.Empty);

    private void OnProfilesChanged(object? sender, EventArgs e) => ProfilesChanged?.Invoke(this, EventArgs.Empty);

    private void OnOptionsChanged(object? sender, EventArgs e) => OptionsChanged?.Invoke(this, EventArgs.Empty);

    private void OnCustomEffectsChanged(object? sender, EventArgs e) => CustomEffectsChanged?.Invoke(this, EventArgs.Empty);

    private CustomEffectLibrary CustomEffectsOrThrow() =>
        _customEffects ?? throw new InvalidOperationException("This engine keeps no own effects.");

    // On the render thread at each step of a count, and on an IPC thread at a change; the state is
    // only read for a listener. Two threads can send their states in another order than the
    // changes; ZoneCountInfo.Sequence lets the app drop the older one.
    private void OnCountChanged(object? sender, EventArgs e)
    {
        if (ZoneCountChanged is { } handler)
        {
            handler(this, _lighting.Count);
        }
    }

    // Nothing is copied on the render thread; PreviewFrames copies the latest output later.
    private void OnRendered(object? sender, EventArgs e)
    {
        if (FrameRendered is not null)
        {
            _frames.Rendered();
        }
    }
}
