using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// The lighting plus the options the engine only reads at startup.
internal sealed record EngineSettings : LightingStateSettings
{
    // False hides the simulated devices, for example once the real ones work through OpenRGB.
    public bool SimulatedDevices { get; set; } = true;

    // Frames per second of animated effects, kept between 1 and LightingController.MaxFramesPerSecond.
    public int FramesPerSecond { get; set; } = LightingController.DefaultFramesPerSecond;

    public static EngineSettings CreateDefault() => new();

    public static EngineSettings From(LightingState state) => CreateDefault().WithLighting(state);

    // A copy with the lighting of state. It starts from this, so every startup option, also one
    // added later, stays as it is.
    public EngineSettings WithLighting(LightingState state)
    {
        var settings = this with { };
        settings.CopyFrom(state);
        return settings;
    }
}
