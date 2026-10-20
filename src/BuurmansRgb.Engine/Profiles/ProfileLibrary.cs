using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// The saved profiles. The file is read on first use, so an engine that nobody asks for profiles
// never reads it, and written on every change. Called from IPC threads; the lock guards the list.
// An invalid or unknown name throws ArgumentException, which reaches the UI as an RPC error.
internal sealed class ProfileLibrary(IProfileStore store, LightingController lighting)
{
    private readonly Lock _gate = new();
    private List<Profile>? _profiles;

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

    // Saves the lighting as it is now. A profile with the same name is replaced and takes the new
    // spelling. The list only changes once the file is written.
    public void Save(string name)
    {
        var normalized = ProfileNames.Normalize(name) ?? throw new ArgumentException($"'{name}' is not a valid profile name.", nameof(name));
        var profile = new Profile(normalized, lighting.State);
        lock (_gate)
        {
            List<Profile> profiles = [.. ProfilesLocked().Where(other => !Matches(other, normalized)), profile];
            profiles.Sort((left, right) => ProfileNames.Comparer.Compare(left.Name, right.Name));
            Store(profiles);
        }
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
