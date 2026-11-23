using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Sharing;

// Strict, for JSON from someone else (SharedFile): no name twice in one object, no null where a
// value is required, and colors only as "#RRGGBB". A field the engine does not know is skipped, so a
// scene of a newer version, with a field this one lacks (such as "overlay" for a version before the
// layer), still comes in without it; nothing that is skipped reaches the engine.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    AllowDuplicateProperties = false,
    RespectNullableAnnotations = true,
    Converters = [typeof(RgbColorJsonConverter)])]
[JsonSerializable(typeof(SharedFile))]
internal sealed partial class SharingJsonContext : JsonSerializerContext;
