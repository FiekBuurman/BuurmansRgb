using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(RgbColorJsonConverter)])]
[JsonSerializable(typeof(EngineSettings))]
[JsonSerializable(typeof(ProfilesFile))]
internal sealed partial class EngineSettingsJsonContext : JsonSerializerContext;
