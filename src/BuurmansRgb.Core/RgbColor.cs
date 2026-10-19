namespace BuurmansRgb.Core;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Black => default;

    public static RgbColor White => new(255, 255, 255);

    public RgbColor Scale(double brightness)
    {
        var factor = Math.Clamp(brightness, 0.0, 1.0);
        return new(ScaleChannel(R, factor), ScaleChannel(G, factor), ScaleChannel(B, factor));
    }

    public static RgbColor FromHsv(double hue, double saturation, double value)
    {
        var h = hue - Math.Floor(hue);
        var s = Math.Clamp(saturation, 0.0, 1.0);
        var v = Math.Clamp(value, 0.0, 1.0);

        var scaled = h * 6.0;
        var sector = (int)scaled;
        var fraction = scaled - sector;

        var p = v * (1.0 - s);
        var q = v * (1.0 - s * fraction);
        var t = v * (1.0 - s * (1.0 - fraction));

        return sector switch
        {
            0 => FromUnit(v, t, p),
            1 => FromUnit(q, v, p),
            2 => FromUnit(p, v, t),
            3 => FromUnit(p, q, v),
            4 => FromUnit(t, p, v),
            _ => FromUnit(v, p, q),
        };
    }

    public static RgbColor Lerp(RgbColor from, RgbColor to, double amount)
    {
        var t = Math.Clamp(amount, 0.0, 1.0);
        return new(LerpChannel(from.R, to.R, t), LerpChannel(from.G, to.G, t), LerpChannel(from.B, to.B, t));
    }

    private static byte ScaleChannel(byte channel, double factor) => (byte)Math.Round(channel * factor);

    private static byte LerpChannel(byte from, byte to, double t) => (byte)Math.Round(from + (to - from) * t);

    private static RgbColor FromUnit(double r, double g, double b) =>
        new((byte)Math.Round(r * 255.0), (byte)Math.Round(g * 255.0), (byte)Math.Round(b * 255.0));
}
