using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// All the lighting as it is written to a file: the global lighting plus, under "devices", the
// devices that differ from it and, under "ledColors", the stored colors of the per-LED effect.
internal record LightingStateSettings : LightingSettings
{
    // Null instead of empty, so the default settings compare equal and stay out of the file.
    public Dictionary<string, LightingSettings?>? Devices { get; set; }

    // One "#RRGGBB" per LED, by device, or null for an LED that follows the device color. Null for
    // the same reason as Devices.
    public Dictionary<string, RgbColor?[]?>? LedColors { get; set; }

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

        var ledColors = new Dictionary<string, IReadOnlyList<RgbColor?>>();
        foreach (var (deviceId, colors) in LedColors ?? [])
        {
            if (colors is not null)
            {
                ledColors[deviceId] = colors;
            }
        }

        return new(ToDeviceLighting(), devices, ledColors);
    }

    protected void CopyFrom(LightingState state)
    {
        CopyFrom(state.Global);
        Devices = state.Devices.Count > 0
            ? state.Devices.ToDictionary(pair => pair.Key, pair => (LightingSettings?)From(pair.Value))
            : null;
        LedColors = state.LedColors.Count > 0
            ? state.LedColors.ToDictionary(pair => pair.Key, pair => (RgbColor?[]?)[.. pair.Value])
            : null;
    }
}
