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

    // "effectColors": by effect id, the own colors of the effects with a list of them
    // (EffectColors.Takes). Null instead of empty, so the default settings stay out of the file; a
    // color that does not read is null (SettingsOptionalColorJsonConverter) and left out.
    public Dictionary<string, RgbColor?[]?>? EffectColors { get; set; }

    // "direction": in degrees (EffectDirection), null or left out along the LEDs. Read as a number
    // with a fraction too, so a direction written by hand as 45.5 is not left out.
    public double? Direction { get; set; }

    // False for a device that is switched off.
    public bool On { get; set; } = DeviceLighting.Default.On;

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
            EffectColors = ReadEffectColors(),
            On = On,
            Direction = ReadDirection(Direction),
        };

    private static double ClampBrightness(double brightness) =>
        double.IsNaN(brightness) ? DeviceLighting.Default.Brightness : Math.Clamp(brightness, 0.0, 1.0);

    private static double ClampSpeed(double speed) =>
        double.IsNaN(speed) ? DeviceLighting.Default.Speed : Math.Clamp(speed, EffectSpeed.Min, EffectSpeed.Max);

    // A direction outside one turn, such as -90 or 450, is the same way within it; a fraction is
    // rounded to whole degrees.
    private static int? ReadDirection(double? degrees) =>
        degrees is { } value && double.IsFinite(value) ? (int)(((Math.Round(value) % 360.0) + 360.0) % 360.0) : null;

    // A list of an effect without a list of its own (or an unknown one), and a list without colors
    // or with too many, is left out: that effect has its default colors.
    private EffectColors ReadEffectColors()
    {
        var lists = new Dictionary<string, IReadOnlyList<RgbColor>>(StringComparer.Ordinal);
        foreach (var (effectId, colors) in EffectColors ?? [])
        {
            RgbColor[] read = [.. (colors ?? []).OfType<RgbColor>()];
            if (Contracts.EffectColors.Takes(effectId) && Contracts.EffectColors.IsValidList(read))
            {
                lists[effectId] = read;
            }
        }

        return new EffectColors(lists);
    }

    protected void CopyFrom(DeviceLighting lighting)
    {
        Color = lighting.Color;
        Brightness = lighting.Brightness;
        Effect = lighting.EffectId;
        Speed = lighting.Speed;
        ColorLoop = ColorLoopSettings.From(lighting.Loop);
        EffectColors = lighting.EffectColors.Lists.Count > 0
            ? lighting.EffectColors.Lists.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => (RgbColor?[]?)[.. pair.Value.Select(color => (RgbColor?)color)])
            : null;
        On = lighting.On;
        Direction = lighting.Direction;
    }
}
