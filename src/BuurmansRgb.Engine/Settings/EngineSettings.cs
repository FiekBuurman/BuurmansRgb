using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// The lighting plus the options of the engine besides it.
internal sealed record EngineSettings : LightingStateSettings
{
    // When the simulated devices show; in the file as a word (SimulatedDevicesModeJsonConverter).
    public SimulatedDevicesMode SimulatedDevices { get; set; } = SimulatedDevicesMode.Automatic;

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
