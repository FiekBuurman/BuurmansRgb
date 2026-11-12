using System.Diagnostics;
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
    SettingsFiles? files = null) : IEngineHost, ISettingsTransfer, IDisposable
{
    // One backup or restore at a time, also from two settings apps.
    private readonly Lock _transferGate = new();

    private LightingSettingsSaver? _saver;
    private SettingsReloader? _reloader;

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

        _saver = new LightingSettingsSaver(settingsStore, lighting, settings, saveDelay ?? LightingSettingsSaver.DefaultDelay, saveEvents: saveEvents);
        if (startScenes == StartScenesOutcome.Written)
        {
            // Saved as any option, so it is in settings.json also when nothing else changes.
            _saver.ChangeOptions(saved => saved with { StartScenesCreated = true });
        }

        simulatedDevices?.ModeChanged += OnSimulatedDevicesModeChanged;
        simulatedDevices?.HadRealDevicesChanged += OnHadRealDevicesChanged;
        if (simulatedDevices?.HadRealDevices == true && !settings.HadRealDevices)
        {
            // A real device was in the first list already, before anyone listened.
            OnHadRealDevicesChanged(this, EventArgs.Empty);
        }

        if (Volatile.Read(ref _autoStartChosen))
        {
            // Chosen in the tray menu while the devices were fetched, before the saver was there.
            OnAutoStartChosen(this, EventArgs.Empty);
        }

        zoneLengths?.LengthsChanged += OnZoneLengthsChanged;
        profiles?.ActiveChanged += OnActiveSceneChanged;
        lighting.FramesPerSecondChanged += OnFramesPerSecondChanged;
        autoPause?.EnabledChanged += OnAutoPauseEnabledChanged;
        lighting.PausedLightsChanged += OnPausedLightsChanged;
        lighting.ZonesAlikeChanged += OnZonesAlikeChanged;
        profileTimer?.CatchUpChanged += OnCatchUpChanged;
        hotkeys?.EnabledChanged += OnHotkeysEnabledChanged;
        customColors?.Changed += OnCustomColorsChanged;
        otherRgbSoftware?.DismissedChanged += OnOtherRgbSoftwareDismissed;
        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Start();
        }

        profileTimer?.CatchUpAtStart();
        if (settingsFileEvents is not null)
        {
            _reloader = new SettingsReloader(settingsFileEvents, settingsStore, ApplyChangedByHand, reloadDelay ?? SettingsReloader.DefaultDelay, holdSaves: _saver.HoldSaves);
        }

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
        simulatedDevices?.ModeChanged -= OnSimulatedDevicesModeChanged;
        simulatedDevices?.HadRealDevicesChanged -= OnHadRealDevicesChanged;
        zoneLengths?.LengthsChanged -= OnZoneLengthsChanged;
        profiles?.ActiveChanged -= OnActiveSceneChanged;
        lighting.FramesPerSecondChanged -= OnFramesPerSecondChanged;
        autoPause?.EnabledChanged -= OnAutoPauseEnabledChanged;
        lighting.PausedLightsChanged -= OnPausedLightsChanged;
        lighting.ZonesAlikeChanged -= OnZonesAlikeChanged;
        profileTimer?.CatchUpChanged -= OnCatchUpChanged;
        hotkeys?.EnabledChanged -= OnHotkeysEnabledChanged;
        customColors?.Changed -= OnCustomColorsChanged;
        otherRgbSoftware?.DismissedChanged -= OnOtherRgbSoftwareDismissed;
        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Dispose();
        }

        _saver?.Dispose();
    }

    // "Instellingen bewaren…": the changes that still wait are written first, so the zip has what
    // the engine has now. The files are read as they are, without the stores: each is written as a
    // whole by a move (JsonFile), so a read gets the old or the new one, never half of one.
    public byte[] Export()
    {
        var known = FilesOrThrow();
        lock (_transferGate)
        {
            FlushSaves();
            return SettingsArchive.Create(known.Read());
        }
    }

    // "Instellingen terugzetten…": nothing changes until the archive passed every check
    // (SettingsArchive.Read) and the files of now are in a zip in the backups folder. Then the own
    // effects, the scenes and the settings are replaced in that order, each through the part of
    // the engine that keeps it, so no save of what the engine held before comes over them: the
    // libraries drop what waited, and settings.json goes in as a change by hand, which the
    // reloader applies at once (ReloadNow), as an edit in an editor, while the saver waits for
    // it. A file that cannot be written, or not read again, is put back with the ones before it.
    public SettingsRestoreResult Restore(byte[] archive)
    {
        var known = FilesOrThrow();
        var content = SettingsArchive.Read(archive);
        if (content.Error is { } refused)
        {
            Trace.TraceWarning($"A backup was not restored: {refused.Outcome}{(refused.FileName is null ? "" : $" ({refused.FileName})")}.");
            return refused;
        }

        lock (_transferGate)
        {
            FlushSaves();
            var before = known.Read();
            try
            {
                known.WriteBackup(before);
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceError($"A backup was not restored: the files of now could not be copied first: {exception}");
                return new SettingsRestoreResult(SettingsRestoreOutcome.Failed);
            }

            // What the files change on the engine besides themselves: an own effect the backup has
            // not makes a device static, and a scene it has not goes off.
            var lightingBefore = lighting.State;
            var activeBefore = profiles?.ActiveName;
            var replaced = new List<string>();
            try
            {
                foreach (var name in SettingsBackup.FileNames)
                {
                    // Added before Replace: a file that was written but could not be read again
                    // goes back too, and putting back one that was not written yet changes nothing.
                    if (content.Files.TryGetValue(name, out var json))
                    {
                        replaced.Add(name);
                        Replace(known, name, json);
                    }
                }
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceError($"Restoring a backup failed at {replaced[^1]}; putting back {string.Join(", ", replaced)}: {exception}");
                PutBack(known, replaced, before, lightingBefore, activeBefore);
                return new SettingsRestoreResult(SettingsRestoreOutcome.Failed);
            }

            Trace.TraceInformation($"Restored a backup: {string.Join(", ", replaced)}.");
            return new SettingsRestoreResult(SettingsRestoreOutcome.Restored);
        }
    }

    private SettingsFiles FilesOrThrow() =>
        files ?? throw new InvalidOperationException("This engine has no settings files to back up.");

    // Between reloads: a save during one waits for it, and would not be in the file yet.
    private void FlushSaves()
    {
        if (_saver is { } saver)
        {
            if (_reloader is { } reloader)
            {
                reloader.RunBetweenReloads(saver.Flush);
            }
            else
            {
                saver.Flush();
            }
        }

        customEffects?.Flush();
    }

    private void Replace(SettingsFiles known, string name, byte[] json)
    {
        switch (name)
        {
            case SettingsBackup.EffectsFileName when customEffects is not null:
                customEffects.Replace(json);
                break;
            case SettingsBackup.ProfilesFileName when profiles is not null:
                profiles.Replace(json);
                break;
            case SettingsBackup.SettingsFileName:
                ReplaceSettings(json);
                break;
            default:
                JsonFile.Write(known.PathOf(name), json);
                break;
        }
    }

    // A file that was not there before the restore goes again, through the part of the engine that
    // keeps it; a file without its part of the engine (settings.json, or any in tests) is just
    // deleted. For settings.json only the file goes, the engine keeps its settings; hardly ever
    // reached, as LoadOrCreate writes it at the start and it is restored last.
    private void Remove(SettingsFiles known, string name)
    {
        switch (name)
        {
            case SettingsBackup.EffectsFileName when customEffects is not null:
                customEffects.Reset();
                break;
            case SettingsBackup.ProfilesFileName when profiles is not null:
                profiles.Reset();
                break;
            default:
                File.Delete(known.PathOf(name));
                break;
        }
    }

    // With autoStartChosen, hadRealDevices and startScenesCreated of this pc
    // (SettingsArchive.WithThisPc): the Run key stays as it is, what the user chose about it here
    // still counts at the next update, and the start scenes do not come again on this pc.
    private void ReplaceSettings(byte[] json)
    {
        var saver = _saver ?? throw new InvalidOperationException("The engine has not started yet.");
        var options = saver.Options;
        settingsStore.Replace(SettingsArchive.WithThisPc(json, options.AutoStartChosen, options.HadRealDevices || simulatedDevices?.HadRealDevices == true, options.StartScenesCreated));
        if (_reloader is not null)
        {
            // Normally applied when this returns; a file that cannot be read yet only later.
            _reloader.ReloadNow();
            return;
        }

        // Without a watcher (in tests) there is no reloader, and so nobody else in HoldSaves.
        saver.HoldSaves(() =>
        {
            if (settingsStore.LoadIfChanged().Settings is { } settings)
            {
                ApplyChangedByHand(settings);
            }
        });
    }

    // Best effort: a file that was not there before goes again, so the engine is as it was; one
    // that cannot be put back stays as the backup had it, and the copy is in the backups folder.
    // Then the lighting and the scene of before go back on, as changes, so the saver keeps them:
    // the files of before have the own effects and the scene again. One that could not be put
    // back has not, and the lighting then shows static instead (LightingController.Apply), or no
    // scene is on.
    private void PutBack(SettingsFiles known, List<string> replaced, Dictionary<string, byte[]> before, LightingState lightingBefore, string? activeBefore)
    {
        foreach (var name in replaced)
        {
            try
            {
                if (before.TryGetValue(name, out var json))
                {
                    Replace(known, name, json);
                }
                else
                {
                    Remove(known, name);
                }
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceError($"Could not put {name} back; the copy is in {known.BackupsFolder}: {exception.Message}");
            }
        }

        // A new lighting ends the scene, so the scene comes after it.
        if (!EngineSettings.SameLighting(lightingBefore, lighting.State))
        {
            lighting.Replace(lightingBefore);
        }

        if (profiles is not null && !string.Equals(profiles.ActiveName, activeBefore, StringComparison.Ordinal))
        {
            profiles.PutBackActive(activeBefore);
        }
    }

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
        simulatedDevices?.Mode = settings.SimulatedDevices;
        simulatedDevices?.HadRealDevices = settings.HadRealDevices;
        lighting.FramesPerSecond = settings.FramesPerSecond;
        autoPause?.Enabled = settings.PauseOnLockAndSleep;
        lighting.PausedLights = settings.PausedLights;
        lighting.ZonesAlike = settings.ReadZonesAlike();
        profileTimer?.CatchUp = settings.CatchUpSceneTimes;
        hotkeys?.Enabled = settings.Hotkeys;
        customColors?.Colors = settings.ReadCustomColors();
        otherRgbSoftware?.Dismissed = settings.ReadDismissedOtherRgbSoftware();
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

    // Reads the newest mode, so two changes that cross each other still save the last one.
    private void OnSimulatedDevicesModeChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { SimulatedDevices = simulatedDevices!.Mode });

    // The fact that a real device was in the list, so it still counts after a restart.
    private void OnHadRealDevicesChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { HadRealDevices = simulatedDevices!.HadRealDevices });

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

    private void OnZonesAlikeChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings.WithZonesAlike(lighting.ZonesAlike));

    private void OnCatchUpChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { CatchUpSceneTimes = profileTimer!.CatchUp });

    private void OnHotkeysEnabledChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { Hotkeys = hotkeys!.Enabled });

    private void OnCustomColorsChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings.WithCustomColors(customColors!.Colors));

    // A program the user dismissed in the app, so its message stays away after a restart.
    private void OnOtherRgbSoftwareDismissed(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings.WithDismissedOtherRgbSoftware(otherRgbSoftware!.Dismissed));
}
