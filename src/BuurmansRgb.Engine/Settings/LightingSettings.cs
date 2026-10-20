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

    // An unknown or missing effect falls back to static and the brightness is kept between
    // 0 and 1, so an old or edited file still loads into what the IPC would accept.
    public DeviceLighting ToDeviceLighting() =>
        new(Color, ClampBrightness(Brightness), EffectCatalog.IsKnown(Effect) ? Effect : EffectIds.Static);

    private static double ClampBrightness(double brightness) =>
        double.IsNaN(brightness) ? DeviceLighting.Default.Brightness : Math.Clamp(brightness, 0.0, 1.0);

    protected void CopyFrom(DeviceLighting lighting)
    {
        Color = lighting.Color;
        Brightness = lighting.Brightness;
        Effect = lighting.EffectId;
    }
}
