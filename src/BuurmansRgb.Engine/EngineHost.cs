using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(
    ISettingsStore settingsStore,
    LightingController lighting,
    EngineIpcServer? ipcServer = null,
    TimeSpan? saveDelay = null,
    SimulatedDevicesSwitch? simulatedDevices = null,
    Action? trimMemory = null,
    SettingsSaveEvents? saveEvents = null,
    IReadOnlyList<IProfileTrigger>? profileTriggers = null,
    ZoneLengthKeeper? zoneLengths = null,
    AutoPause? autoPause = null,
    ProfileLibrary? profiles = null,
    ProfileTimer? profileTimer = null,
    AllOnOffHotkeys? hotkeys = null,
    ISettingsFileEvents? settingsFileEvents = null,
    TimeSpan? reloadDelay = null) : IEngineHost, IDisposable
{
    private LightingSettingsSaver? _saver;
    private SettingsReloader? _reloader;

    // The IPC server starts once the lights are on and keeps running until ct is cancelled.
    // trimMemory then runs once, so the memory that only starting up needed goes back to Windows.
    // The zone lengths are known before the devices are fetched, so a device gets its length as it
    // shows up. From then on a new mode for the simulated devices and a zone length the user sets
    // are saved like the lighting, and the profile triggers may load profiles: after the saved
    // lighting, so they are not overwritten, and with the saver, so what they load is saved. The
    // scene that was on is on again with the saved lighting, as that is still its lighting; once
    // all triggers run, the profile timer (also one of them) may catch up on the scene of the time.
    // With settingsFileEvents, a change of the file by hand is applied from then on as well, as
    // changes from the settings app are.
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        ApplyOptions(settings);
        zoneLengths?.Load(settings.ReadZoneLengths());
        await lighting.DiscoverAsync(ct);
        lighting.Apply(settings.ToLightingState());
        if (profiles is not null)
        {
            profiles.RestoreActive(settings.ActiveScene);
            settings = settings with { ActiveScene = profiles.ActiveName };
        }

        _saver = new LightingSettingsSaver(settingsStore, lighting, settings, saveDelay ?? LightingSettingsSaver.DefaultDelay, saveEvents: saveEvents);
        simulatedDevices?.ModeChanged += OnSimulatedDevicesModeChanged;
        zoneLengths?.LengthsChanged += OnZoneLengthsChanged;
        profiles?.ActiveChanged += OnActiveSceneChanged;
        lighting.FramesPerSecondChanged += OnFramesPerSecondChanged;
        autoPause?.EnabledChanged += OnAutoPauseEnabledChanged;
        lighting.PausedLightsChanged += OnPausedLightsChanged;
        profileTimer?.CatchUpChanged += OnCatchUpChanged;
        hotkeys?.EnabledChanged += OnHotkeysEnabledChanged;
        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Start();
        }

        profileTimer?.CatchUpAtStart();
        if (settingsFileEvents is not null)
        {
            _reloader = new SettingsReloader(settingsFileEvents, settingsStore, ApplyChangedByHand, reloadDelay ?? SettingsReloader.DefaultDelay, holdSaves: _saver.HoldSaves);
        }

        _ = ipcServer?.RunAsync(ct);
        trimMemory?.Invoke();
    }

    public PauseReasons Pause => lighting.Pause;

    public event EventHandler<PauseReasons>? PauseChanged
    {
        add => lighting.PauseChanged += value;
        remove => lighting.PauseChanged -= value;
    }

    public void SetPaused(bool paused) => lighting.SetPaused(PauseReasons.Manual, paused);

    public void Dispose()
    {
        _reloader?.Dispose();
        simulatedDevices?.ModeChanged -= OnSimulatedDevicesModeChanged;
        zoneLengths?.LengthsChanged -= OnZoneLengthsChanged;
        profiles?.ActiveChanged -= OnActiveSceneChanged;
        lighting.FramesPerSecondChanged -= OnFramesPerSecondChanged;
        autoPause?.EnabledChanged -= OnAutoPauseEnabledChanged;
        lighting.PausedLightsChanged -= OnPausedLightsChanged;
        profileTimer?.CatchUpChanged -= OnCatchUpChanged;
        hotkeys?.EnabledChanged -= OnHotkeysEnabledChanged;
        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Dispose();
        }

        _saver?.Dispose();
    }

    // settings.json changed by hand: applied as the settings app would, through the same
    // properties, so the app follows (OptionsChanged, LightingChanged, DevicesChanged) and the saver
    // keeps the new values. The saver adopts them as what the file holds, so it does not write the
    // file back in its own words; the reloader reads and applies them within HoldSaves of the
    // saver. The lighting is only replaced when it is another, so a change of an option alone
    // leaves the scene on and the effects running. The scene that is on stays the engine's: a
    // changed activeScene loads nothing, and a new lighting ends the scene as any change does.
    private void ApplyChangedByHand(EngineSettings settings)
    {
        try
        {
            ApplyOptions(settings);
            zoneLengths?.Change(settings.ReadZoneLengths());
            var state = settings.ToLightingState();
            if (!EngineSettings.SameLighting(state, lighting.State))
            {
                lighting.Replace(state);
            }
        }
        finally
        {
            // Also after a failure halfway: the file is still the user's to keep.
            _saver!.Adopt(settings);
        }
    }

    // The options besides the lighting and the zone lengths, at the start and after a change by hand.
    private void ApplyOptions(EngineSettings settings)
    {
        simulatedDevices?.Mode = settings.SimulatedDevices;
        lighting.FramesPerSecond = settings.FramesPerSecond;
        autoPause?.Enabled = settings.PauseOnLockAndSleep;
        lighting.PausedLights = settings.PausedLights;
        profileTimer?.CatchUp = settings.CatchUpSceneTimes;
        hotkeys?.Enabled = settings.Hotkeys;
    }

    // Reads the newest mode, so two changes that cross each other still save the last one.
    private void OnSimulatedDevicesModeChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { SimulatedDevices = simulatedDevices!.Mode });

    // Also reads the newest lengths, for the same reason.
    private void OnZoneLengthsChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings.WithZoneLengths(zoneLengths!.Lengths));

    // And the scene that is on now.
    private void OnActiveSceneChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { ActiveScene = profiles!.ActiveName });

    // The options from the settings app, also the newest value each.
    private void OnFramesPerSecondChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { FramesPerSecond = lighting.FramesPerSecond });

    private void OnAutoPauseEnabledChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { PauseOnLockAndSleep = autoPause!.Enabled });

    private void OnPausedLightsChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { PausedLights = lighting.PausedLights });

    private void OnCatchUpChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { CatchUpSceneTimes = profileTimer!.CatchUp });

    private void OnHotkeysEnabledChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { Hotkeys = hotkeys!.Enabled });
}
