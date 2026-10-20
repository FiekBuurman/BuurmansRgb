using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// Setters instead of init: the JSON source generator sets every init property, which would
// replace the defaults below with zero when a property is missing from the file.
internal record LightingSettings
{
    public RgbColor Color { get; set; } = DeviceLighting.Default.Color;

    public double Brightness { get; set; } = DeviceLighting.Default.Brightness;

    public string? Effect { get; set; } = DeviceLighting.Default.EffectId;

    public double Speed { get; set; } = DeviceLighting.Default.Speed;

    // Null for the default loop.
    public ColorLoopSettings? ColorLoop { get; set; }

    public static LightingSettings From(DeviceLighting lighting)
    {
        var settings = new LightingSettings();
        settings.CopyFrom(lighting);
        return settings;
    }

    // An unknown or missing effect falls back to static and brightness and speed are kept within
    // their range, so an old or edited file still loads into what the IPC would accept.
    public DeviceLighting ToDeviceLighting() =>
        new(Color, ClampBrightness(Brightness), EffectCatalog.IsKnown(Effect) ? Effect : EffectIds.Static, ClampSpeed(Speed))
        {
            Loop = ColorLoop?.ToColorLoop() ?? Contracts.ColorLoop.Default,
        };

    private static double ClampBrightness(double brightness) =>
        double.IsNaN(brightness) ? DeviceLighting.Default.Brightness : Math.Clamp(brightness, 0.0, 1.0);

    private static double ClampSpeed(double speed) =>
        double.IsNaN(speed) ? DeviceLighting.Default.Speed : Math.Clamp(speed, EffectSpeed.Min, EffectSpeed.Max);

    protected void CopyFrom(DeviceLighting lighting)
    {
        Color = lighting.Color;
        Brightness = lighting.Brightness;
        Effect = lighting.EffectId;
        Speed = lighting.Speed;
        ColorLoop = ColorLoopSettings.From(lighting.Loop);
    }
}
