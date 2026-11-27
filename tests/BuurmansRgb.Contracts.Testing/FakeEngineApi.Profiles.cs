using System.Globalization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Testing;

// Scenes (profiles in the code): saving, loading and deleting, their lighting, when they load by
// themselves, the devices they are for, and the times of "Hele huis".
public sealed partial class FakeEngineApi
{
    public event EventHandler? ProfilesChanged;

    public void RaiseProfilesChanged() => ProfilesChanged?.Invoke(this, EventArgs.Empty);

    // Like the engine: saving adds the name, deleting removes it, in alphabetical order.
    public List<string> Profiles { get; } = [];

    // The lighting a loaded profile shows, by profile name.
    public Dictionary<string, LightingInfo> ProfileLighting { get; } = [];

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Profiles]);

    // Like the engine: the profile saved or loaded last; tests set it for a scene from the tray.
    public string? ActiveProfile { get; set; }

    public Task<string?> GetActiveProfileAsync(CancellationToken ct = default) => Task.FromResult(ActiveProfile);

    public async Task SaveProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile save {name}");
        // Like the engine, a scene with the same name is replaced; it takes the new spelling.
        Profiles.RemoveAll(other => SceneNames.Comparer.Equals(other, name));
        Profiles.Add(name);
        Profiles.Sort(StringComparer.CurrentCultureIgnoreCase);
        ActiveProfile = name;
    }

    public async Task LoadProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile load {name}");
        if (ProfileLighting.TryGetValue(name, out var lighting))
        {
            Lighting.Clear();
            Lighting[""] = lighting;
        }

        ActiveProfile = name;
    }

    public async Task DeleteProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile delete {name}");
        Profiles.Remove(name);
        if (ActiveProfile == name)
        {
            ActiveProfile = null;
        }
    }

    // Profiles that are gone by the time they are asked, such as deleted from the tray menu.
    public HashSet<string> GoneProfiles { get; } = [];

    // Like the engine: the lighting a profile gives all devices; the default lighting without one.
    public Task<LightingInfo> GetProfileLightingAsync(string name, CancellationToken ct = default) =>
        GoneProfiles.Contains(name)
            ? Task.FromException<LightingInfo>(new ArgumentException($"There is no scene '{name}'.", nameof(name)))
            : Task.FromResult(ProfileLighting.GetValueOrDefault(name, new LightingInfo(RgbColor.White, 1.0, EffectIds.Static)));

    // By profile name; a profile without one never loads by itself.
    public Dictionary<string, SceneAutomation> ProfileAutomations { get; } = [];

    public Task<SceneAutomation> GetProfileAutomationAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(ProfileAutomations.GetValueOrDefault(name, SceneAutomation.None));

    // What came in, as it came in, before Normalize.
    public List<(string Name, SceneAutomation Automation)> ProfileAutomationCalls { get; } = [];

    // Like the engine: saved as Normalize gives it.
    public async Task SetProfileAutomationAsync(string name, SceneAutomation automation, CancellationToken ct = default)
    {
        ProfileAutomationCalls.Add((name, automation));
        await Record($"profile automation {name} {(automation.Times.Count == 0 ? "-" : string.Join(",", automation.Times.Select(time => time.At.ToString("HH:mm", CultureInfo.InvariantCulture))))} {(automation.Programs.Count == 0 ? "-" : string.Join(",", automation.Programs))}");
        ProfileAutomations[name] = automation.Normalize();
    }

    // By profile name, the devices it is for; a profile without an entry is for every device.
    public Dictionary<string, IReadOnlyList<string>> ProfileDevices { get; } = [];

    public Task<IReadOnlyList<string>?> GetProfileDevicesAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(ProfileDevices.GetValueOrDefault(name));

    // Like the engine: saved as SceneDevices.Normalize gives them; no device throws.
    public async Task SetProfileDevicesAsync(string name, IReadOnlyList<string>? deviceIds, CancellationToken ct = default)
    {
        await Record($"profile devices {name} {(deviceIds is null ? "all" : string.Join(",", deviceIds))}");
        if (deviceIds is null)
        {
            ProfileDevices.Remove(name);
            return;
        }

        var normalized = SceneDevices.Normalize(deviceIds);
        ProfileDevices[name] = normalized.Count > 0 ? normalized : throw new ArgumentException("A scene is for at least one device.", nameof(deviceIds));
    }

    // "Uit om" and "Aan om"; OffScene stays as the test gives it, as the engine keeps the scene.
    public WholeHouseTimes WholeHouseTimes { get; set; } = WholeHouseTimes.None;

    // Such as the engine that cannot be reached while the app reads the times.
    public Exception? GetWholeHouseTimesException { get; set; }

    public Task<WholeHouseTimes> GetWholeHouseTimesAsync(CancellationToken ct = default) =>
        GetWholeHouseTimesException is { } exception
            ? Task.FromException<WholeHouseTimes>(exception)
            : Task.FromResult(WholeHouseTimes);

    // What came in, as it came in, before Normalize.
    public List<WholeHouseTimes> WholeHouseTimesCalls { get; } = [];

    // Like the engine: saved as Normalize gives the times.
    public async Task SetWholeHouseTimesAsync(WholeHouseTimes times, CancellationToken ct = default)
    {
        WholeHouseTimesCalls.Add(times);
        await Record($"whole house off {WriteTimes(times.Off)} on {WriteTimes(times.On)}");
        WholeHouseTimes = new WholeHouseTimes(new SceneAutomation(times.Off).Normalize().Times, new SceneAutomation(times.On).Normalize().Times, WholeHouseTimes.OffScene);
    }

    private static string WriteTimes(IReadOnlyList<LoadTime> times) =>
        times.Count == 0 ? "-" : string.Join(",", times.Select(time => time.At.ToString("HH:mm", CultureInfo.InvariantCulture) + (time.Days == WeekDays.Every ? string.Empty : $"/{time.Days}")));
}
