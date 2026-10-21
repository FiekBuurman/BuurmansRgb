using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(SettingsColorJsonConverter), typeof(SettingsOptionalColorJsonConverter), typeof(SimulatedDevicesModeJsonConverter), typeof(ZoneLengthsJsonConverter), typeof(LedColorZoneLengthsJsonConverter)])]
[JsonSerializable(typeof(EngineSettings))]
[JsonSerializable(typeof(ProfilesFile))]
internal sealed partial class EngineSettingsJsonContext : JsonSerializerContext;
