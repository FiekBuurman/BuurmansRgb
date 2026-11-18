using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Sharing;

// Strict, for JSON from someone else (SharedFile): no field the engine does not know, no name twice
// in one object, no null where a value is required, and colors only as "#RRGGBB".
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false,
    RespectNullableAnnotations = true,
    Converters = [typeof(RgbColorJsonConverter)])]
[JsonSerializable(typeof(SharedFile))]
internal sealed partial class SharingJsonContext : JsonSerializerContext;
