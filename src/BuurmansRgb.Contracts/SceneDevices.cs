namespace BuurmansRgb.Contracts;

// The rules for the devices a scene is for (IEngineApi.SetProfileDevicesAsync), shared so the UI
// sends what the engine saves. A device counts by its id (DeviceInfo.Id), which stays the same over
// a restart of OpenRGB or the pc.
public static class SceneDevices
{
    // The ids in their order, each once, without one that is null or empty.
    public static List<string> Normalize(IEnumerable<string?> deviceIds)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. deviceIds.Where(id => !string.IsNullOrWhiteSpace(id) && seen.Add(id)).Select(id => id!)];
    }
}
