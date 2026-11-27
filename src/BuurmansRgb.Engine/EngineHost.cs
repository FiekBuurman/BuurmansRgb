using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Programs;
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
    TimeSpan? reloadDelay = null,
    CustomColorList? customColors = null,
    CustomEffectLibrary? customEffects = null,
    AutoStartSwitch? autoStart = null,
    bool firstRun = false,
    OtherRgbSoftwareCheck? otherRgbSoftware = null,
    SettingsFolder? files = null) : IEngineHost, ISettingsTransfer, IDisposable
{
    private readonly SettingsTransfer _transfer = new(settingsStore, lighting, files, simulatedDevices, profiles, customEffects);

    private EngineSettingsSaver? _saver;
    private SettingsReloader? _reloader;

    // The options the saver keeps besides the lighting (CreateOptions), made at their first use in
    // StartAsync, as a field initializer cannot use the host.
    private OptionBinding[]? _options;

    // Set once the user chose autostart (AutoStartChosen); read when the saver comes, for a
    // choice before it.
    private bool _autoStartChosen;

    // The IPC server starts once the lights are on and keeps running until ct is cancelled.
    // trimMemory then runs once, so the memory that only starting up needed goes back to Windows.
    // The zone lengths are known before the devices are fetched, so a device gets its length as it
    // shows up. From then on a new mode for the simulated devices and a zone length the user sets
    // are saved like the lighting, and the profile triggers may load profiles: after the saved
    // lighting, so they are not overwritten, and with the saver, so what they load is saved. The
    // scene that was on is on again with the saved lighting, as that is still its lighting; once
    // all triggers run, the profile timer (also one of them) may catch up on the scene of the time.
    // With settingsFileEvents, a change of the file by hand is applied from then on as well, as
    // changes from the settings app are. The own effects are read before the lighting is applied, so
    // a lighting that shows one keeps it. With firstRun (--first-run, after every install) autostart
    // goes on, unless the user chose it before (autoStartChosen); a file from before that option
    // gets it from the Run key first (StartAutoStart). That is not saved by itself: it goes along
    // with the next save, and until then the Run key tells the same at each start. A save of its
    // own would write the defaults over a file that did not read (locked, say).
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        settings = StartAutoStart(settings);
        ApplyOptions(settings);
        zoneLengths?.Load(settings.ReadZoneLengths());
        await lighting.DiscoverAsync(ct);
        customEffects?.Load();
        lighting.Apply(settings.ToLightingState());
        var startScenes = StartScenesOutcome.AlreadySettled;
        if (profiles is not null)
        {
            (settings, startScenes) = StartScenesOnce(settings, profiles, settingsStore.LoadDidNotRead);
            profiles.RestoreActive(settings.ActiveScene);
            settings = settings with { ActiveScene = profiles.ActiveName };
        }

        _saver = new EngineSettingsSaver(settingsStore, lighting, settings, saveDelay ?? EngineSettingsSaver.DefaultDelay, saveEvents: saveEvents);
        if (startScenes == StartScenesOutcome.Written)
        {
            // The saver has startScenesCreated already (StartScenesOnce put it in settings); this
            // only schedules a save, as any option gets, so it is in settings.json also when
            // nothing else changes. Setting it again changes nothing, but says what the save is for.
            _saver.ChangeOptions(saved => saved with { StartScenesCreated = true });
        }

        foreach (var option in Options)
        {
            option.Listen();
        }

        if (simulatedDevices?.HadRealDevices == true && !settings.HadRealDevices)
        {
            // A real device was in the first list already, before anyone listened.
            _saver.ChangeOptions(SaveHadRealDevices);
        }

        if (Volatile.Read(ref _autoStartChosen))
        {
            // Chosen in the tray menu while the devices were fetched, before the saver was there.
            OnAutoStartChosen(this, EventArgs.Empty);
        }

        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Start();
        }

        profileTimer?.CatchUpAtStart();
        if (settingsFileEvents is not null)
        {
            _reloader = new SettingsReloader(settingsFileEvents, settingsStore, ApplyChangedByHand, reloadDelay ?? SettingsReloader.DefaultDelay, holdSaves: _saver.HoldSaves);
        }

        _transfer.Start(_saver, _reloader, ApplyChangedByHand);

        // Once, before the settings app can ask; after waking up and at "Verversen" the check
        // itself and EngineApi look again.
        otherRgbSoftware?.Check();
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
        autoStart?.Chosen -= OnAutoStartChosen;
        foreach (var option in _options ?? [])
        {
            option.StopListening();
        }

        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Dispose();
        }

        _saver?.Dispose();
    }

    // "Instellingen bewaren…" and "terugzetten…", which SettingsTransfer does.
    public byte[] Export() => _transfer.Export();

    public SettingsRestoreResult Restore(byte[] archive) => _transfer.Restore(archive);

    // settings.json changed by hand: applied as the settings app would, through the same
    // properties, so the app follows (OptionsChanged, LightingChanged, DevicesChanged) and the
    // saver keeps the new values. The saver adopts them as what the file holds, so it does not
    // write the file back in its own words; the reloader reads and applies them within HoldSaves
    // of the saver. The lighting is only replaced when it is another, so a change of an option
    // alone leaves the scene on and the effects running. The scene that is on stays the engine's:
    // a changed activeScene loads nothing, and a new lighting ends the scene as any change does.
    // The saver gets the lighting as this left it, the one compared with the file or what Replace
    // put on, so a change in the app meanwhile is not taken as in the file but saved.
    private void ApplyChangedByHand(EngineSettings settings)
    {
        var held = lighting.State;
        try
        {
            // Nothing on the engine follows it, so it goes to the saver as the file has it.
            _saver!.ChangeOptions(saved => saved with { AutoStartChosen = settings.AutoStartChosen });
            ApplyOptions(settings);
            zoneLengths?.Change(settings.ReadZoneLengths());
            var state = settings.ToLightingState();
            held = lighting.State;
            if (!EngineSettings.SameLighting(state, held))
            {
                held = lighting.Replace(state);
            }
        }
        finally
        {
            // Also after a failure halfway: the file is still the user's to keep.
            _saver!.Adopt(settings, held);
        }
    }

    // The options besides the lighting and zone lengths, at the start and after a change by hand.
    private void ApplyOptions(EngineSettings settings)
    {
        foreach (var option in Options)
        {
            option.Apply?.Invoke(settings);
        }
    }

    // Before the devices are fetched, so the tray menu shows it from the start. A file without
    // autoStartChosen is from before the option, whose installer turned autostart on, so off
    // counts as the user's choice (or Windows refused, which this cannot tell). The Run key can
    // only tell for this engine: one that starts another engine counts as off. A Run key that does
    // not read tells nothing, so it stays without a choice (and --first-run tries to turn it on).
    private EngineSettings StartAutoStart(EngineSettings settings)
    {
        if (autoStart is null)
        {
            return settings;
        }

        autoStart.Chosen += OnAutoStartChosen;
        if (settings.AutoStartChosen is null && autoStart.TryReadEnabled() is { } enabled)
        {
            settings = settings with { AutoStartChosen = !enabled };
        }

        if (firstRun && settings.AutoStartChosen != true)
        {
            autoStart.EnableOnFirstRun();
        }

        return settings;
    }

    // A new installation gets the start scenes, once (startScenesCreated): only without a
    // profiles.json, so a pc from before this option that has one, also with all its scenes
    // deleted, gets none. Once the scenes are written, StartAsync has the saver write
    // startScenesCreated right after the start, not with whatever change comes next: the user may
    // delete the scenes, which does not save settings.json, and then profiles.json too. A
    // settings.json that was there but did not read (settingsDidNotRead: locked by a virus scanner
    // or OneDrive, say) decides nothing: its startScenesCreated is not known, and a save of its own
    // would, after the saver's waits for a change by hand, write the defaults over the user's file.
    // So no scenes and no save then; a next start that reads it decides.
    // A file that was there already settles it as well, but goes along with the next save, as
    // autoStartChosen does, so a start without changes still writes nothing; until then that
    // profiles.json tells the same.
    private static (EngineSettings Settings, StartScenesOutcome Outcome) StartScenesOnce(EngineSettings settings, ProfileLibrary library, bool settingsDidNotRead)
    {
        if (settings.StartScenesCreated)
        {
            return (settings, StartScenesOutcome.AlreadySettled);
        }

        if (settingsDidNotRead)
        {
            return (settings, StartScenesOutcome.NotWritten);
        }

        var outcome = library.AddStartScenes(StartScenes.Create());
        return (outcome == StartScenesOutcome.NotWritten ? settings : settings with { StartScenesCreated = true }, outcome);
    }

    // From the tray menu or the settings app, on any thread.
    private void OnAutoStartChosen(object? sender, EventArgs e)
    {
        Volatile.Write(ref _autoStartChosen, true);
        _saver?.ChangeOptions(settings => settings with { AutoStartChosen = true });
    }

    private OptionBinding[] Options => _options ??= CreateOptions();

    // Every option the saver keeps besides the lighting, in one list, so StartAsync listens to
    // each, Dispose stops listening to each and ApplyOptions sets each in this order. Each save
    // reads the newest value, so two changes that cross each other still save the last one. The
    // zone lengths and the scene that is on have no apply: StartAsync and ApplyChangedByHand set
    // them their own way.
    private OptionBinding[] CreateOptions() =>
    [
        Bind(
            handler => simulatedDevices?.ModeChanged += handler,
            handler => simulatedDevices?.ModeChanged -= handler,
            settings => simulatedDevices?.Mode = settings.SimulatedDevices,
            settings => settings with { SimulatedDevices = simulatedDevices!.Mode }),
        Bind(
            handler => simulatedDevices?.HadRealDevicesChanged += handler,
            handler => simulatedDevices?.HadRealDevicesChanged -= handler,
            settings => simulatedDevices?.HadRealDevices = settings.HadRealDevices,
            SaveHadRealDevices),
        Bind(
            handler => zoneLengths?.LengthsChanged += handler,
            handler => zoneLengths?.LengthsChanged -= handler,
            apply: null,
            settings => settings.WithZoneLengths(zoneLengths!.Lengths)),
        Bind(
            handler => profiles?.ActiveChanged += handler,
            handler => profiles?.ActiveChanged -= handler,
            apply: null,
            settings => settings with { ActiveScene = profiles!.ActiveName }),

        // The options from the settings app.
        Bind(
            handler => lighting.FramesPerSecondChanged += handler,
            handler => lighting.FramesPerSecondChanged -= handler,
            settings => lighting.FramesPerSecond = settings.FramesPerSecond,
            settings => settings with { FramesPerSecond = lighting.FramesPerSecond }),
        Bind(
            handler => autoPause?.EnabledChanged += handler,
            handler => autoPause?.EnabledChanged -= handler,
            settings => autoPause?.Enabled = settings.PauseOnLockAndSleep,
            settings => settings with { PauseOnLockAndSleep = autoPause!.Enabled }),
        Bind(
            handler => lighting.PausedLightsChanged += handler,
            handler => lighting.PausedLightsChanged -= handler,
            settings => lighting.PausedLights = settings.PausedLights,
            settings => settings with { PausedLights = lighting.PausedLights }),
        Bind(
            handler => lighting.SceneFadeChanged += handler,
            handler => lighting.SceneFadeChanged -= handler,
            settings => lighting.SceneFade = settings.ReadSceneFade(),
            settings => settings.WithSceneFade(lighting.SceneFade)),
        Bind(
            handler => lighting.ZonesAlikeChanged += handler,
            handler => lighting.ZonesAlikeChanged -= handler,
            settings => lighting.ZonesAlike = settings.ReadZonesAlike(),
            settings => settings.WithZonesAlike(lighting.ZonesAlike)),
        Bind(
            handler => profileTimer?.CatchUpChanged += handler,
            handler => profileTimer?.CatchUpChanged -= handler,
            settings => profileTimer?.CatchUp = settings.CatchUpSceneTimes,
            settings => settings with { CatchUpSceneTimes = profileTimer!.CatchUp }),
        Bind(
            handler => hotkeys?.EnabledChanged += handler,
            handler => hotkeys?.EnabledChanged -= handler,
            settings => hotkeys?.Enabled = settings.Hotkeys,
            settings => settings with { Hotkeys = hotkeys!.Enabled }),
        Bind(
            handler => customColors?.Changed += handler,
            handler => customColors?.Changed -= handler,
            settings => customColors?.Colors = settings.ReadCustomColors(),
            settings => settings.WithCustomColors(customColors!.Colors)),

        // A program the user dismissed in the app, so its message stays away after a restart.
        Bind(
            handler => otherRgbSoftware?.DismissedChanged += handler,
            handler => otherRgbSoftware?.DismissedChanged -= handler,
            settings => otherRgbSoftware?.Dismissed = settings.ReadDismissedOtherRgbSoftware(),
            settings => settings.WithDismissedOtherRgbSoftware(otherRgbSoftware!.Dismissed)),
    ];

    // The fact that a real device was in the list, so it still counts after a restart.
    private EngineSettings SaveHadRealDevices(EngineSettings settings) =>
        settings with { HadRealDevices = simulatedDevices!.HadRealDevices };

    // A change of the option, on any thread, goes to the saver once it is there.
    private OptionBinding Bind(Action<EventHandler> listen, Action<EventHandler> stopListening, Action<EngineSettings>? apply, Func<EngineSettings, EngineSettings> save) =>
        new(listen, stopListening, apply, (_, _) => _saver?.ChangeOptions(save));

    // One option: how the host listens to its event, and how a settings file sets it (Apply).
    private sealed record OptionBinding(Action<EventHandler> Add, Action<EventHandler> Remove, Action<EngineSettings>? Apply, EventHandler Changed)
    {
        public void Listen() => Add(Changed);

        public void StopListening() => Remove(Changed);
    }
}
