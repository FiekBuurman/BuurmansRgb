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
// ArgumentException, which reaches the UI as an RPC error.
//
// It also knows which scene is on (ActiveName): the one loaded or saved last, until the lighting
// changes in any other way. That is not kept over a restart of the engine.
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

    // In alphabetical order.
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
            {
                return [.. ProfilesLocked().Select(profile => profile.Name)];
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
                return [.. ProfilesLocked().Where(profile => profile.Automation != ProfileAutomation.None).Select(profile => (profile.Name, profile.Automation))];
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
            return [];
        }
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
            var old = ProfilesLocked().Find(other => Matches(other, normalized));
            var profile = new Profile(normalized, lightingNow) { Automation = old?.Automation ?? ProfileAutomation.None };
            List<Profile> profiles = [.. ProfilesLocked().Where(other => !ReferenceEquals(other, old)), profile];
            profiles.Sort((left, right) => ProfileNames.Comparer.Compare(left.Name, right.Name));
            Store(profiles);
        }

        lock (_activeGate)
        {
            _activeName = normalized;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Shows the lighting of the profile and saves it as the current lighting.
    public void Load(string name)
    {
        Profile profile;
        lock (_gate)
        {
            profile = FindLocked(name);
        }

        Show(profile.Lighting, profile.Name);
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

    public void Delete(string name)
    {
        string deleted;
        lock (_gate)
        {
            var profile = FindLocked(name);
            Store([.. ProfilesLocked().Where(other => !ReferenceEquals(other, profile))]);
            deleted = profile.Name;
        }

        lock (_activeGate)
        {
            if (_activeName is not null && Matches(deleted, _activeName))
            {
                _activeName = null;
            }
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

    private static bool Matches(Profile profile, string name) => Matches(profile.Name, name);

    private static bool Matches(string left, string right) => ProfileNames.Comparer.Equals(left, right);

    private void OnLightingChanged(object? sender, EventArgs e)
    {
        lock (_activeGate)
        {
            _activeName = _showingThread == Environment.CurrentManagedThreadId ? _showingName : null;
        }
    }

    private List<Profile> ProfilesLocked() => _profiles ??= [.. _store.Load().OrderBy(profile => profile.Name, ProfileNames.Comparer)];

    private Profile FindLocked(string name) =>
        ProfilesLocked().Find(profile => Matches(profile, name?.Trim() ?? string.Empty))
        ?? throw new ArgumentException($"There is no profile '{name}'.", nameof(name));

    private void Store(List<Profile> profiles)
    {
        _store.Save(profiles);
        _profiles = profiles;
    }
}
