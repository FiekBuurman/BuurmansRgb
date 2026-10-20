using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

// Setters instead of init: the JSON source generator sets every init property, which would
// replace the defaults below with zero when a property is missing from the file.
internal sealed record EngineSettings
{
    public static EngineSettings CreateDefault() => new();

    public RgbColor Color { get; set; } = RgbColor.White;

    public double Brightness { get; set; } = 1.0;
}
