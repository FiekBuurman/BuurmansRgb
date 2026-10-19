using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// Setters instead of init: the JSON source generator sets every init property, which would
// replace the defaults below with zero when a property is missing from the file.
internal record LightingSettings
{
    public RgbColor Color { get; set; } = RgbColor.White;

    public double Brightness { get; set; } = 1.0;

    public string? Effect { get; set; } = EffectIds.Static;

    public static LightingSettings From(DeviceLighting lighting)
    {
        var settings = new LightingSettings();
        settings.CopyFrom(lighting);
        return settings;
    }

    // An unknown or missing effect falls back to static, so an old or edited file still loads.
    public DeviceLighting ToDeviceLighting() =>
        new(Color, Brightness, EffectCatalog.IsKnown(Effect) ? Effect! : EffectIds.Static);

    protected void CopyFrom(DeviceLighting lighting)
    {
        Color = lighting.Color;
        Brightness = lighting.Brightness;
        Effect = lighting.EffectId;
    }
}
