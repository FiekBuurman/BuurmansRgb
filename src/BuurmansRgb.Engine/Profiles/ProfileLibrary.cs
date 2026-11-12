using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Profiles;

// The saved profiles, all of them kept in memory once read (about 5 KB for a profile with a color
// per LED for 250 LEDs). The file is read on first use: when the tray menu opens or the settings
// app asks, and right at the start when the file exists, because the profile triggers need the
// times and programs then. Written on every change. Called from the tray thread, IPC threads and
// the timers of the triggers; the lock guards the list. An invalid or unknown name throws
// ArgumentException, which reaches the UI as an RPC error. The internal profiles of "Uit om" and
// "Aan om" (Profile.IsInternal) are in the same file, but no scenes: no name, no load, save or
// delete by name finds them.
//
// It also knows which scene is on (ActiveName): the one loaded or saved last, until the lighting
// changes in any other way. The engine keeps it over a restart in settings.json (RestoreActive).
internal sealed class ProfileLibrary
{
    private readonly IProfileStore _store;
    private readonly LightingController _lighting;
    private readonly Lock _gate = new();
    private List<Profile>? _profiles;

    // Guards the scene that is on. Apart from _gate, because LightingChanged comes from inside the
    // lighting controller, also while another thread holds _gate.
    private readonly Lock _activeGate = new();
    private string? _activeName;

    // The scene that Show is putting on, and its thread: the LightingChanged of that thread is the
    // scene coming on, any other is a change that ends it.
    private string? _showingName;
    private int _showingThread;

    public ProfileLibrary(IProfileStore store, LightingController lighting)
    {
        _store = store;
        _lighting = lighting;
        lighting.LightingChanged += OnLightingChanged;
    }

    // Raised after the list was written: a profile was saved or deleted, or got another automation.
    // Outside the lock, so a listener can read the library again.
    public event EventHandler? Changed;

    // Raised when another scene is on, or none. Outside the locks.
    public event EventHandler? ActiveChanged;

    // In alphabetical order.
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
            {
                return [.. ScenesLocked().Select(profile => profile.Name)];
            }
        }
    }

    // The name of the scene that is on, in its saved spelling; null when none is.
    public string? ActiveName
    {
        get
        {
            lock (_activeGate)
            {
                return _activeName;
            }
        }
    }

    // The profiles that load by themselves, in alphabetical order. None when profiles.json cannot
    // be read, so whoever loads them keeps running; the next change reads the file again.
    public IReadOnlyList<(string Name, ProfileAutomation Automation)> TryGetAutomated()
    {
        try
        {
            lock (_gate)
            {
                return [.. ScenesLocked().Where(profile => profile.Automation != ProfileAutomation.None).Select(profile => (profile.Name, profile.Automation))];
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
            return [];
        }
    }

    // The scenes of a new installation (StartScenes), written only when there is no profiles.json
    // yet. A file, also one with an empty list, is from a pc that had scenes, or whose user
    // deleted them all, and stays as it is. NotWritten when it could not be written now, so a next
    // start tries again; the engine runs on without them.
    public StartScenesOutcome AddStartScenes(IReadOnlyList<Profile> scenes)
    {
        try
        {
            lock (_gate)
            {
                if (_store.Exists)
                {
                    return StartScenesOutcome.FileThere;
                }

                Store([.. scenes.OrderBy(profile => profile.Name, ProfileNames.Comparer)]);
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError($"Could not write the start scenes; the next start tries again: {exception}");
            return StartScenesOutcome.NotWritten;
        }

        Trace.TraceInformation($"A new installation: made the start scenes {string.Join(", ", scenes.Select(scene => $"'{scene.Name}'"))}.");
        Changed?.Invoke(this, EventArgs.Empty);
        return StartScenesOutcome.Written;
    }

    // Saves the lighting as it is now. A profile with the same name is replaced and takes the new
    // spelling, but keeps its automation. The list only changes once the file is written. What
    // burns then is that scene, so it is the one that is on.
    public void Save(string name)
    {
        var normalized = ProfileNames.Normalize(name) ?? throw new ArgumentException($"'{name}' is not a valid profile name.", nameof(name));
        var lightingNow = _lighting.State;
        lock (_gate)
        {
            var old = ScenesLocked().FirstOrDefault(other => Matches(other, normalized));
            var profile = new Profile(normalized, lightingNow) { Automation = old?.Automation ?? ProfileAutomation.None, Role = old?.Role ?? ProfileRole.None };
            List<Profile> profiles = [.. ProfilesLocked().Where(other => !ReferenceEquals(other, old)), profile];
            profiles.Sort((left, right) => ProfileNames.Comparer.Compare(left.Name, right.Name));
            Store(profiles);
        }

        SetActive(normalized);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // The scene that was on when the engine stopped, from settings.json, once the saved lighting is
    // back on. Only a scene that still exists, in its saved spelling; null or an unknown name
    // leaves none on. Raises no ActiveChanged: nothing changed since the scene was saved.
    public void RestoreActive(string? name)
    {
        var restored = SavedNameOf(name);
        lock (_activeGate)
        {
            _activeName = restored;
        }
    }

    // The scene that was on before a restore that failed (EngineHost.Restore), once the files of
    // before are back: as RestoreActive, but as a change (ActiveChanged), so it is saved again.
    public void PutBackActive(string? name) => SetActive(SavedNameOf(name));

    // The saved spelling of a scene that still exists; null for null, an unknown name, or a file
    // that cannot be read.
    private string? SavedNameOf(string? name)
    {
        if (name is null)
        {
            return null;
        }

        try
        {
            lock (_gate)
            {
                return ScenesLocked().FirstOrDefault(profile => Matches(profile, name.Trim()))?.Name;
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
            return null;
        }
    }

    // Shows the lighting of the profile and saves it as the current lighting. For "Alles uit" (the
    // role AllOff) the lighting it replaces is kept first, for "Aan om".
    public void Load(string name)
    {
        var lightingNow = _lighting.State;
        Profile profile;
        lock (_gate)
        {
            profile = FindLocked(name);
            if (profile.Role == ProfileRole.AllOff)
            {
                RememberBeforeOffLocked(profile, new HeldLighting(lightingNow, ActiveName));
            }
        }

        Show(profile.Lighting, profile.Name);
    }

    // The scenes with times and then the internal profiles of "Uit om" and "Aan om" with times, for
    // ProfileTimer; the internal one of "Uit om" only while there is no scene "Alles uit", whose
    // times count instead. None when profiles.json cannot be read, as TryGetAutomated.
    public IReadOnlyList<TimedProfile> TryGetTimed()
    {
        try
        {
            lock (_gate)
            {
                var hasAllOffScene = AllOffSceneLocked() is not null;
                return
                [
                    .. ScenesLocked().Where(profile => profile.Automation.Times.Count > 0).Select(profile => new TimedProfile(profile.Name, profile.Role, IsInternal: false, profile.Automation.Times)),
                    .. ProfilesLocked()
                        .Where(profile => profile.IsInternal && profile.Automation.Times.Count > 0 && !(profile.Role == ProfileRole.AllOff && hasAllOffScene))
                        .OrderBy(profile => profile.Role)
                        .Select(profile => new TimedProfile(profile.Name, profile.Role, IsInternal: true, profile.Automation.Times)),
                ];
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
            return [];
        }
    }

    // What a time of timed does, unless hold keeps it for when the programs stop (ProgramHold):
    // - a scene comes on, as Load;
    // - the internal "Uit om" switches every device off and keeps its lighting, as "Alles uit" in
    //   the tray menu; nothing while every device is off already;
    // - "Aan om" only does something while every device is off, so a choice made since stays. After
    //   "Alles uit" the lighting from before it comes back as it was (BackOnOver); after another way
    //   off, such as "Alles uit" in the tray menu, every device goes on with its own lighting, as
    //   "Alles aan". While a program shows its scene, that goes for what comes back once it stops:
    //   after "Uit om" held during a game, the devices go on when the game stops in the morning.
    // A scene that is gone throws ArgumentException.
    public void LoadTimed(TimedProfile timed, ProgramHold? hold = null)
    {
        if (!timed.IsInternal)
        {
            if (hold?.Invoke(held => HeldScene(timed.Name, held)) != true)
            {
                Load(timed.Name);
            }

            return;
        }

        if (timed.Role == ProfileRole.AllOff)
        {
            // Asked first, so a program whose own scene has every device off still gets the lighting
            // from before it off; only without a program it looks at the lighting now.
            if (hold?.Invoke(held => held.Lighting.IsAnyOn ? new HeldLighting(held.Lighting.WithOn(false), null) : held) != true && _lighting.State.IsAnyOn)
            {
                Trace.TraceInformation("Uit om: every device goes off.");
                _lighting.SetLighting(null, lighting => lighting with { On = false });
            }

            return;
        }

        if (hold?.Invoke(held => BackOnOver(held) ?? held) == true)
        {
            Trace.TraceInformation("Aan om: a program shows its scene, so it counts for what comes back once it stops.");
            return;
        }

        if (BackOnOver(new HeldLighting(_lighting.State, ActiveName)) is not { } backOn)
        {
            Trace.TraceInformation("Aan om: a device is on already, so the lighting stays as it is.");
            return;
        }

        Trace.TraceInformation("Aan om: the devices go on.");
        Show(backOn.Lighting, backOn.Scene);
    }

    // The scene of a time while a program holds it (ProgramHold). For "Alles uit" what would come
    // back is kept first, as when it loads.
    private HeldLighting HeldScene(string name, HeldLighting held)
    {
        lock (_gate)
        {
            var profile = FindLocked(name);
            if (profile.Role == ProfileRole.AllOff)
            {
                RememberBeforeOffLocked(profile, held);
            }

            return new HeldLighting(profile.Lighting, profile.Name);
        }
    }

    // What "Aan om" puts on over off (the lighting with the scene that is on with it): after "Alles
    // uit" the lighting from before it as it was, so a device that was off then (a fan) stays off;
    // else, or when nothing was on before it either, that lighting with every device on. Without a
    // scene, as only the lighting from before is kept (RememberBeforeOffLocked). Null while a device
    // is on.
    private HeldLighting? BackOnOver(HeldLighting off)
    {
        if (off.Lighting.IsAnyOn)
        {
            return null;
        }

        LightingState? before;
        lock (_gate)
        {
            before = AllOffSceneLocked() is { } scene && off.Scene is { } active && Matches(scene.Name, active)
                ? InternalLocked(ProfileRole.BackOn)?.Lighting
                : null;
        }

        return new HeldLighting(before is { IsAnyOn: true } ? before : (before ?? off.Lighting).WithOn(true), Scene: null);
    }

    // "Uit om" and "Aan om" as they are saved.
    public WholeHouseTimes GetWholeHouseTimes()
    {
        lock (_gate)
        {
            var scene = AllOffSceneLocked();
            return new WholeHouseTimes((scene ?? InternalLocked(ProfileRole.AllOff))?.Automation.Times, InternalLocked(ProfileRole.BackOn)?.Automation.Times, scene?.Name);
        }
    }

    // "Uit om" goes on the scene "Alles uit" while there is one, which keeps its program, and else on
    // an internal profile; "Aan om" on an internal one, which starts with the lighting as it is now
    // until "Alles uit" comes on. Without times an internal profile goes from the file. Throws
    // ArgumentException for more than ProfileAutomation.MaxTimes times.
    public void SetWholeHouseTimes(WholeHouseTimes times)
    {
        ArgumentNullException.ThrowIfNull(times);
        var off = new ProfileAutomation(times.Off).Normalize();
        var on = new ProfileAutomation(times.On).Normalize();
        var lightingNow = _lighting.State;
        lock (_gate)
        {
            var scene = AllOffSceneLocked();
            var backOn = InternalLocked(ProfileRole.BackOn);
            List<Profile> profiles =
            [
                .. ScenesLocked().Select(profile => ReferenceEquals(profile, scene) ? profile with { Automation = profile.Automation with { Times = off.Times } } : profile),
            ];
            if (scene is null && off.Times.Count > 0)
            {
                profiles.Add(Profile.Internal(ProfileRole.AllOff, StartScenes.AllOff, off));
            }

            if (on.Times.Count > 0)
            {
                profiles.Add(Profile.Internal(ProfileRole.BackOn, backOn?.Lighting ?? lightingNow, on));
            }

            profiles.Sort((left, right) => ProfileNames.Comparer.Compare(left.Name, right.Name));
            Store(profiles);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Shows lighting and saves it as the current lighting, with scene as the scene that is on, or
    // null for none: for example the lighting from before a game, which may have been a scene.
    // Outside _gate: the controller has its own lock.
    public void Show(LightingState state, string? scene)
    {
        lock (_activeGate)
        {
            _showingName = scene;
            _showingThread = Environment.CurrentManagedThreadId;
        }

        try
        {
            _lighting.Replace(state);
        }
        finally
        {
            lock (_activeGate)
            {
                _showingName = null;
                _showingThread = 0;
            }
        }
    }

    public LightingState GetLighting(string name)
    {
        lock (_gate)
        {
            return FindLocked(name).Lighting;
        }
    }

    // Deleting "Alles uit" keeps "Uit om": its times go to an internal profile, which switches every
    // device off from then on.
    public void Delete(string name)
    {
        string deleted;
        lock (_gate)
        {
            var profile = FindLocked(name);
            List<Profile> profiles = [.. ProfilesLocked().Where(other => !ReferenceEquals(other, profile))];
            if (profile.Role == ProfileRole.AllOff && profile.Automation.Times.Count > 0 && !profiles.Exists(other => !other.IsInternal && other.Role == ProfileRole.AllOff))
            {
                profiles.RemoveAll(other => other.IsInternal && other.Role == ProfileRole.AllOff);
                profiles.Add(Profile.Internal(ProfileRole.AllOff, StartScenes.AllOff, new ProfileAutomation(profile.Automation.Times)));
                profiles.Sort((left, right) => ProfileNames.Comparer.Compare(left.Name, right.Name));
            }

            Store(profiles);
            deleted = profile.Name;
        }

        bool activeChanged;
        lock (_activeGate)
        {
            activeChanged = _activeName is not null && Matches(deleted, _activeName) && SetActiveLocked(null);
        }

        if (activeChanged)
        {
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ProfileAutomation GetAutomation(string name)
    {
        lock (_gate)
        {
            return FindLocked(name).Automation;
        }
    }

    public void SetAutomation(string name, ProfileAutomation automation)
    {
        ArgumentNullException.ThrowIfNull(automation);
        var normalized = automation.Normalize();
        lock (_gate)
        {
            var profile = FindLocked(name);
            Store([.. ProfilesLocked().Select(other => ReferenceEquals(other, profile) ? other with { Automation = normalized } : other)]);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // profiles.json from a backup (EngineHost.Restore), a file that reads: written as the file and
    // read again, so the triggers plan the new times and programs (Changed). The scene that is on
    // stays on while the backup has a scene of that name. The list in memory is only dropped once
    // the file is written, so a write that fails leaves the scenes as they were.
    public void Replace(byte[] json) => ReplaceFile(() => _store.Replace(json));

    // profiles.json taken away again, when a restore that fails puts back the files of before and
    // there was none: no scenes then, and the one that is on is off. As with Replace, a delete that
    // fails changes nothing.
    public void Reset() => ReplaceFile(_store.Delete);

    private void ReplaceFile(Action write)
    {
        List<string> names;
        lock (_gate)
        {
            write();
            _profiles = null;
            names = [.. ScenesLocked().Select(profile => profile.Name)];
        }

        bool activeGone;
        lock (_activeGate)
        {
            activeGone = _activeName is { } active && !names.Exists(name => Matches(name, active)) && SetActiveLocked(null);
        }

        if (activeGone)
        {
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool Matches(Profile profile, string name) => Matches(profile.Name, name);

    private static bool Matches(string left, string right) => ProfileNames.Comparer.Equals(left, right);

    private void OnLightingChanged(object? sender, EventArgs e)
    {
        bool changed;
        lock (_activeGate)
        {
            changed = SetActiveLocked(_showingThread == Environment.CurrentManagedThreadId ? _showingName : null);
        }

        if (changed)
        {
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetActive(string? name)
    {
        bool changed;
        lock (_activeGate)
        {
            changed = SetActiveLocked(name);
        }

        if (changed)
        {
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // True when that is another scene than before.
    private bool SetActiveLocked(string? name)
    {
        var changed = !string.Equals(_activeName, name, StringComparison.Ordinal);
        _activeName = name;
        return changed;
    }

    // All of them, the internal ones too (Profile.IsInternal).
    private List<Profile> ProfilesLocked() => _profiles ??= [.. _store.Load().OrderBy(profile => profile.Name, ProfileNames.Comparer)];

    // The scenes of the user: the profiles with a name, in alphabetical order.
    private IEnumerable<Profile> ScenesLocked() => ProfilesLocked().Where(profile => !profile.IsInternal);

    // The scene "Alles uit", whose times are "Uit om"; the first in alphabetical order should a file
    // edited by hand have more.
    private Profile? AllOffSceneLocked() => ScenesLocked().FirstOrDefault(profile => profile.Role == ProfileRole.AllOff);

    private Profile? InternalLocked(ProfileRole role) => ProfilesLocked().Find(profile => profile.IsInternal && profile.Role == role);

    // "Aan om" puts back the lighting that burned before "Alles uit" came on, from before. Only the
    // lighting is kept, in its internal profile, not the scene that was on (before.Scene only tells
    // whether that was "Alles uit" itself); only while there is one, and not when "Alles uit" was on
    // already, as then that is what burned. Not a change of the scenes, so no Changed. A file that
    // cannot be written keeps what it had, and "Alles uit" still comes on.
    private void RememberBeforeOffLocked(Profile scene, HeldLighting before)
    {
        if (InternalLocked(ProfileRole.BackOn) is not { } backOn || (before.Scene is { } active && Matches(scene.Name, active)))
        {
            return;
        }

        try
        {
            Store([.. ProfilesLocked().Select(profile => ReferenceEquals(profile, backOn) ? profile with { Lighting = before.Lighting } : profile)]);
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError($"Could not keep the lighting from before Alles uit for Aan om: {exception}");
        }
    }

    private Profile FindLocked(string name) =>
        ScenesLocked().FirstOrDefault(profile => Matches(profile, name?.Trim() ?? string.Empty))
        ?? throw new ArgumentException($"There is no profile '{name}'.", nameof(name));

    private void Store(List<Profile> profiles)
    {
        _store.Save(profiles);
        _profiles = profiles;
    }
}
