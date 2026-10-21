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

    // Whether the animation pauses while Windows is locked or asleep (AutoPause).
    public bool PauseOnLockAndSleep { get; set; } = true;

    // "zoneLengths": by device id, the LED count the user gave each resizable zone, by zone name.
    // Null instead of empty, so the default settings stay out of the file.
    public Dictionary<string, Dictionary<string, int>?>? ZoneLengths { get; set; }

    // "activeScene": the name of the scene that is on (ProfileLibrary.ActiveName), so it is still
    // on after a restart; null, and so not in the file, when none is.
    public string? ActiveScene { get; set; }

    public static EngineSettings CreateDefault() => new();

    // The zone lengths as ZoneLengthKeeper takes them; a device without zones (null) is left out.
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ReadZoneLengths() =>
        (ZoneLengths ?? []).Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, int>)pair.Value!);

    public EngineSettings WithZoneLengths(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> lengths) => this with
    {
        ZoneLengths = lengths.Count > 0
            ? lengths.ToDictionary(pair => pair.Key, pair => (Dictionary<string, int>?)new Dictionary<string, int>(pair.Value))
            : null,
    };

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
