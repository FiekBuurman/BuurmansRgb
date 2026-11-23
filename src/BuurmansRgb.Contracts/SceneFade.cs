namespace BuurmansRgb.Contracts;

// How long the lights take to go over to a scene, or to "Alles aan" or "Alles uit": a crossfade
// from the colors they show to the new ones. Zero is at once, as before there was a fade.
public static class SceneFade
{
    public static TimeSpan Max { get; } = TimeSpan.FromSeconds(2);

    public static TimeSpan Default { get; } = TimeSpan.FromSeconds(0.5);

    // Kept between zero and Max.
    public static TimeSpan Clamp(TimeSpan fade) =>
        fade < TimeSpan.Zero ? TimeSpan.Zero : fade > Max ? Max : fade;

    // The same for seconds, as settings.json holds them; NaN, which is no time, is the default.
    // Infinity is kept within the range like any other number.
    public static TimeSpan FromSeconds(double seconds) =>
        double.IsNaN(seconds) ? Default
            : seconds <= 0 ? TimeSpan.Zero
            : seconds >= Max.TotalSeconds ? Max
            : TimeSpan.FromSeconds(seconds);
}
