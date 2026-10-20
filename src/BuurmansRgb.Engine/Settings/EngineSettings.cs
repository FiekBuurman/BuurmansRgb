using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// The global lighting plus, under "devices", the devices that differ from it.
internal sealed record EngineSettings : LightingSettings
{
    // Null instead of empty, so the default settings compare equal and stay out of the file.
    public Dictionary<string, LightingSettings?>? Devices { get; set; }

    public static EngineSettings CreateDefault() => new();

    public static EngineSettings From(LightingState state)
    {
        var settings = new EngineSettings();
        settings.CopyFrom(state.Global);
        if (state.Devices.Count > 0)
        {
            settings.Devices = state.Devices.ToDictionary(pair => pair.Key, pair => (LightingSettings?)From(pair.Value));
        }

        return settings;
    }

    public LightingState ToLightingState()
    {
        var devices = new Dictionary<string, DeviceLighting>();
        foreach (var (deviceId, settings) in Devices ?? [])
        {
            if (settings is not null)
            {
                devices[deviceId] = settings.ToDeviceLighting();
            }
        }

        return new(ToDeviceLighting(), devices);
    }
}
