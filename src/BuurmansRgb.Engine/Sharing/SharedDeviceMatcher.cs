using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Sharing;

// Which device here each device of a shared scene is (SharedDeviceRef): first the one with the
// same id, as on the pc the scene was made on, or one where OpenRGB gives the same model on the same
// place the same id; then, for the others, the device with the same type and name. Two of the same
// type and name, such as two RAM sticks of one kit that OpenRGB names alike, go in their order: the
// first of the scene to the first here that is still free, and so on. A device here gets at most one
// of the scene. What is left is not here.
internal static class SharedDeviceMatcher
{
    // Map has the id here of each device of the scene that is here; Unmatched the others, in order.
    public static (IReadOnlyDictionary<string, string> Map, IReadOnlyList<SharedDeviceRef> Unmatched) Match(
        IReadOnlyList<SharedDeviceRef> shared, IReadOnlyList<RgbDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(devices);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var device in shared)
        {
            if (devices.Any(here => here.Id == device.Id))
            {
                map[device.Id] = device.Id;
                taken.Add(device.Id);
            }
        }

        foreach (var device in shared.Where(device => !map.ContainsKey(device.Id)))
        {
            if (device.Type is { } type && device.Name is { } name
                && devices.FirstOrDefault(here => here.Type == type && here.Name == name && !taken.Contains(here.Id)) is { } match)
            {
                map[device.Id] = match.Id;
                taken.Add(match.Id);
            }
        }

        return (map, [.. shared.Where(device => !map.ContainsKey(device.Id))]);
    }
}
