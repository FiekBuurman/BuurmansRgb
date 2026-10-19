using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// The global lighting plus, under "devices", the devices that differ from it, under "ledColors"
// the stored colors of the per-LED effect, and the options the engine only reads at startup.
internal sealed record EngineSettings : LightingSettings
{
    // False hides the simulated devices, for example once the real ones work through OpenRGB.
    public bool SimulatedDevices { get; set; } = true;

    // Frames per second of animated effects, kept between 1 and LightingController.MaxFramesPerSecond.
    public int FramesPerSecond { get; set; } = LightingController.DefaultFramesPerSecond;

    // Null instead of empty, so the default settings compare equal and stay out of the file.
    public Dictionary<string, LightingSettings?>? Devices { get; set; }

    // One "#RRGGBB" per LED, by device. Null for the same reason as Devices.
    public Dictionary<string, RgbColor[]?>? LedColors { get; set; }

    public static EngineSettings CreateDefault() => new();

    public static EngineSettings From(LightingState state) => CreateDefault().WithLighting(state);

    // A copy with the lighting of state. It starts from this, so every startup option, also one
    // added later, stays as it is.
    public EngineSettings WithLighting(LightingState state)
    {
        var settings = this with
        {
            Devices = state.Devices.Count > 0
                ? state.Devices.ToDictionary(pair => pair.Key, pair => (LightingSettings?)From(pair.Value))
                : null,
            LedColors = state.LedColors.Count > 0
                ? state.LedColors.ToDictionary(pair => pair.Key, pair => (RgbColor[]?)[.. pair.Value])
                : null,
        };
        settings.CopyFrom(state.Global);
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

        var ledColors = new Dictionary<string, IReadOnlyList<RgbColor>>();
        foreach (var (deviceId, colors) in LedColors ?? [])
        {
            if (colors is not null)
            {
                ledColors[deviceId] = colors;
            }
        }

        return new(ToDeviceLighting(), devices, ledColors);
    }
}
