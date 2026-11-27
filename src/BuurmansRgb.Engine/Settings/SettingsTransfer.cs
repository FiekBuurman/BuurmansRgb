using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Settings;

// "Instellingen bewaren…" and "Instellingen terugzetten…" for EngineHost, which makes it with the
// parts that keep the files. The saver and the reloader only come in EngineHost.StartAsync, so
// they come through Start; an Export before then only writes what the own effects still wait
// with, as the saver of settings.json is not there yet.
internal sealed class SettingsTransfer(
    ISettingsStore settingsStore,
    LightingController lighting,
    SettingsFiles? files = null,
    SimulatedDevicesSwitch? simulatedDevices = null,
    ProfileLibrary? profiles = null,
    CustomEffectLibrary? customEffects = null) : ISettingsTransfer
{
    // One backup or restore at a time, also from two settings apps.
    private readonly Lock _gate = new();

    private LightingSettingsSaver? _saver;
    private SettingsReloader? _reloader;
    private Action<EngineSettings>? _applyChangedByHand;

    // In StartAsync, before the IPC server runs, so a settings app that asks finds them.
    // applyChangedByHand is EngineHost.ApplyChangedByHand, which the reloader runs as well.
    public void Start(LightingSettingsSaver saver, SettingsReloader? reloader, Action<EngineSettings> applyChangedByHand)
    {
        _saver = saver;
        _reloader = reloader;
        _applyChangedByHand = applyChangedByHand;
    }

    // "Instellingen bewaren…": the changes that still wait are written first, so the zip has what
    // the engine has now. The files are read as they are, without the stores: each is written as a
    // whole by a move (JsonFile), so a read gets the old or the new one, never half of one.
    public byte[] Export()
    {
        var known = FilesOrThrow();
        lock (_gate)
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

        lock (_gate)
        {
            FlushSaves();
            var before = known.Read();
            try
            {
                known.WriteBackup(before);
            }
            catch (Exception exception) when (FileErrors.IsAccessError(exception))
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
            catch (Exception exception) when (FileErrors.IsAccessError(exception))
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
                _applyChangedByHand!(settings);
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
            catch (Exception exception) when (FileErrors.IsAccessError(exception))
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
}
