using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    Converters = [typeof(RgbColorJsonConverter)])]
[JsonSerializable(typeof(EngineSettings))]
internal sealed partial class EngineSettingsJsonContext : JsonSerializerContext;
