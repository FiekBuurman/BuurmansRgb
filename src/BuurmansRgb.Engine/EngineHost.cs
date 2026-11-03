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
    // How many copies the engine keeps of the files a restored backup replaced.
    internal const int BackupsKept = 5;

    // How often a file that another program holds just then is read again for a backup.
    private const int ReadAttempts = 5;

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
        if (profiles is not null)
        {
            profiles.RestoreActive(settings.ActiveScene);
            settings = settings with { ActiveScene = profiles.ActiveName };
        }

        _saver = new LightingSettingsSaver(settingsStore, lighting, settings, saveDelay ?? LightingSettingsSaver.DefaultDelay, saveEvents: saveEvents);
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
            return SettingsArchive.Create(ReadFiles(known));
        }
    }

    // "Instellingen terugzetten…": nothing changes until the archive passed every check
    // (SettingsArchive.Read) and the files of now are in a zip in the backups folder. Then the own
    // effects, the scenes and the settings are replaced in that order, each through the part of
    // the engine that keeps it, so no save of what the engine held before comes over them: the
    // libraries drop what waited, and settings.json goes in as a change by hand, which the
    // reloader applies at once (ReloadNow), as an edit in an editor, while the saver waits for
    // it. A file that cannot be written puts back the ones replaced before it.
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
            var before = ReadFiles(known);
            try
            {
                WriteBackup(known, before);
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceError($"A backup was not restored: the files of now could not be copied first: {exception}");
                return new SettingsRestoreResult(SettingsRestoreOutcome.Failed);
            }

            var replaced = new List<string>();
            try
            {
                foreach (var name in SettingsBackup.FileNames)
                {
                    if (content.Files.TryGetValue(name, out var json))
                    {
                        Replace(known, name, json);
                        replaced.Add(name);
                    }
                }
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceError($"Restoring a backup failed after {string.Join(", ", replaced.DefaultIfEmpty("no file"))}; putting those back: {exception}");
                PutBack(known, replaced, before);
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

    // The files there are now, by name; one that is not there is left out.
    private static Dictionary<string, byte[]> ReadFiles(SettingsFiles known)
    {
        var read = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in SettingsBackup.FileNames)
        {
            if (ReadFile(known.PathOf(name)) is { } json)
            {
                read.Add(name, json);
            }
        }

        return read;
    }

    // Shared for writing and deleting, so a save of the engine that moves a new file over it at
    // that moment does not fail; a moment later the read is tried again.
    private static byte[]? ReadFile(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    // "backups\before-restore-20261103-231500.zip", the newest BackupsKept of them.
    private static void WriteBackup(SettingsFiles known, Dictionary<string, byte[]> before)
    {
        if (before.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(known.BackupsFolder);
        var path = Path.Combine(known.BackupsFolder, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}{SettingsBackup.Extension}");
        JsonFile.Write(path, SettingsArchive.Create(before));
        foreach (var old in Directory.GetFiles(known.BackupsFolder, "before-restore-*" + SettingsBackup.Extension).Order(StringComparer.Ordinal).SkipLast(BackupsKept))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceWarning($"Could not delete the old backup {old}: {exception.Message}");
            }
        }
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
    // keeps it. settings.json is replaced last, so a failure never has to take it away; a file
    // without its part of the engine (in tests) is just deleted.
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

    // With autoStartChosen and hadRealDevices of this pc (SettingsArchive.WithThisPc): the Run key
    // stays as it is, and what the user chose about it here still counts at the next update.
    private void ReplaceSettings(byte[] json)
    {
        var saver = _saver ?? throw new InvalidOperationException("The engine has not started yet.");
        var options = saver.Options;
        settingsStore.Replace(SettingsArchive.WithThisPc(json, options.AutoStartChosen, options.HadRealDevices || simulatedDevices?.HadRealDevices == true));
        if (_reloader is not null)
        {
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
    private void PutBack(SettingsFiles known, List<string> replaced, Dictionary<string, byte[]> before)
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
