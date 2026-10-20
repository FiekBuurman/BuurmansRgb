using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Profiles;

// The saved profiles. The file is read on first use, so an engine that nobody asks for profiles
// never reads it, and written on every change. Called from IPC threads; the lock guards the list.
// An invalid or unknown name throws ArgumentException, which reaches the UI as an RPC error.
internal sealed class ProfileLibrary(IProfileStore store, LightingController lighting)
{
    private readonly Lock _gate = new();
    private List<Profile>? _profiles;

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
    // spelling, but keeps its automation. The list only changes once the file is written.
    public void Save(string name)
    {
        var normalized = ProfileNames.Normalize(name) ?? throw new ArgumentException($"'{name}' is not a valid profile name.", nameof(name));
        var lightingNow = lighting.State;
        lock (_gate)
        {
            var old = ProfilesLocked().Find(other => Matches(other, normalized));
            var profile = new Profile(normalized, lightingNow) { Automation = old?.Automation ?? ProfileAutomation.None };
            List<Profile> profiles = [.. ProfilesLocked().Where(other => !ReferenceEquals(other, old)), profile];
            profiles.Sort((left, right) => ProfileNames.Comparer.Compare(left.Name, right.Name));
            Store(profiles);
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

        // Outside the lock: the controller has its own.
        lighting.Replace(profile.Lighting);
    }

    public void Delete(string name)
    {
        lock (_gate)
        {
            var profile = FindLocked(name);
            Store([.. ProfilesLocked().Where(other => !ReferenceEquals(other, profile))]);
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

    private static bool Matches(Profile profile, string name) => ProfileNames.Comparer.Equals(profile.Name, name);

    private List<Profile> ProfilesLocked() => _profiles ??= [.. store.Load().OrderBy(profile => profile.Name, ProfileNames.Comparer)];

    private Profile FindLocked(string name) =>
        ProfilesLocked().Find(profile => Matches(profile, name?.Trim() ?? string.Empty))
        ?? throw new ArgumentException($"There is no profile '{name}'.", nameof(name));

    private void Store(List<Profile> profiles)
    {
        store.Save(profiles);
        _profiles = profiles;
    }
}
