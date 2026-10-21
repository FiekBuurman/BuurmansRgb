using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

// "colorLoop" in settings.json. Setters for the same reason as LightingSettings.
internal sealed record ColorLoopSettings
{
    // Null for a color that did not read (SettingsOptionalColorJsonConverter); the loop goes without it.
    public RgbColor?[]? Colors { get; set; }

    public bool Spread { get; set; } = true;

    // Null for the default loop, so it stays out of the file.
    public static ColorLoopSettings? From(ColorLoop loop) =>
        loop == ColorLoop.Default ? null : new() { Colors = [.. loop.Colors.Select(color => (RgbColor?)color)], Spread = loop.Spread };

    // A missing, too short or too long list falls back to the default colors.
    public ColorLoop ToColorLoop()
    {
        var loop = new ColorLoop([.. (Colors ?? []).OfType<RgbColor>()], Spread);
        return loop.IsValid ? loop : ColorLoop.Default with { Spread = Spread };
    }

    public bool Equals(ColorLoopSettings? other) =>
        other is not null
        && Spread == other.Spread
        && (Colors ?? []).SequenceEqual(other.Colors ?? []);

    public override int GetHashCode() => HashCode.Combine(Colors?.Length, Spread);
}
