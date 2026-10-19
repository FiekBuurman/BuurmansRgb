using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(RgbColorJsonConverter)])]
[JsonSerializable(typeof(EngineSettings))]
internal sealed partial class EngineSettingsJsonContext : JsonSerializerContext;
