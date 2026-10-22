using System.Text.Json;
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

    // Whether the animation pauses while Windows is locked or asleep, or the screen is off
    // (AutoPause). The name is from before the screen counted, and stays for settings.json.
    public bool PauseOnLockAndSleep { get; set; } = true;

    // What the lights show during that pause (LightingController.PausedLights); in the file as a
    // word (PausedLightsModeJsonConverter).
    public PausedLightsMode PausedLights { get; set; } = PausedLightsMode.Keep;

    // Whether the scene of the last time that passed comes on after waking up and at the start
    // (ProfileTimer.CatchUp); off, such a time is skipped.
    public bool CatchUpSceneTimes { get; set; }

    // Whether Ctrl+Alt+Shift+A and Ctrl+Alt+Shift+U switch everything on and off (AllOnOffHotkeys).
    public bool Hotkeys { get; set; } = true;

    // "zoneLengths": by device id, the LED count the user gave each resizable zone, by zone name.
    // Null instead of empty, so the default settings stay out of the file.
    public Dictionary<string, Dictionary<string, int>?>? ZoneLengths { get; set; }

    // "zonesAlike": the ids of the devices whose zones show one image (LightingController.ZonesAlike).
    // Null instead of empty, so the default settings stay out of the file; an id that is not
    // text reads as null (LenientJson) and is left out.
    public List<string?>? ZonesAlike { get; set; }

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

    // Without the ids that are null or empty.
    public IReadOnlySet<string> ReadZonesAlike() =>
        new HashSet<string>((ZonesAlike ?? []).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!), StringComparer.Ordinal);

    // In order, so the file only changes when the devices do.
    public EngineSettings WithZonesAlike(IReadOnlySet<string> deviceIds) => this with
    {
        ZonesAlike = deviceIds.Count > 0 ? [.. deviceIds.Order(StringComparer.Ordinal)] : null,
    };

    public static EngineSettings From(LightingState state) => CreateDefault().WithLighting(state);

    // Whether two lightings come out the same in the file. The records hold dictionaries and
    // lists, which compare by reference.
    public static bool SameLighting(LightingState left, LightingState right) =>
        From(left).ToJson().AsSpan().SequenceEqual(From(right).ToJson());

    // A copy with the lighting of state. It starts from this, so every startup option, also one
    // added later, stays as it is.
    public EngineSettings WithLighting(LightingState state)
    {
        var settings = this with { };
        settings.CopyFrom(state);
        return settings;
    }

    // The settings as settings.json holds them.
    public byte[] ToJson() =>
        JsonSerializer.SerializeToUtf8Bytes(this, EngineSettingsJsonContext.Default.EngineSettings);
}
